# Deploying CartCompare

The API runs on Google Cloud Run, the database on Neon, and the frontend on
Vercel, all on free tiers except one part: Kroger's firewall refuses Cloud
Run's shared outgoing addresses, so the API sends its traffic from a fixed
address (step 4b, about $4–5 a month). Run the `gcloud` commands in
[Cloud Shell](https://shell.cloud.google.com), which has `gcloud` and `git`
installed; no local Docker is needed because Cloud Build builds
`backend/Dockerfile`.

Replace the `<...>` placeholders. The commands assume region `us-east1`
(one of Cloud Run's free-tier regions) and the service name `cartcompare-api`.

## 1. Database (Neon)

1. Create a Neon project and copy its connection details from **Connect**.
   Use the direct (not `-pooler`) host.
2. Write them as an Npgsql connection string:

   ```text
   Host=<ep-xxx>.us-east-1.aws.neon.tech;Database=neondb;Username=<user>;Password=<password>;SSL Mode=Require
   ```

The API creates the tables itself on startup (`Database__ApplyMigrations=true`
below).

## 2. Google Cloud project

1. Create a project and link a billing account. Cloud Run's free tier still
   needs one.
2. **Billing → Budgets & alerts**: add a $1 budget with email alerts. There is
   no spending cap, so this is the safety net.
3. In Cloud Shell:

   ```bash
   gcloud config set project <project-id>
   gcloud services enable run.googleapis.com cloudbuild.googleapis.com secretmanager.googleapis.com artifactregistry.googleapis.com
   ```

## 3. Secrets

Store the four secrets in Secret Manager. Each command prompts for the value,
so it is not saved in your shell history. Generate the JWT key with
`openssl rand -base64 48` (it must be at least 32 bytes).

```bash
for name in cartcompare-db cartcompare-jwt-key kroger-client-id kroger-client-secret; do
  read -rsp "$name: " value && echo && printf '%s' "$value" | gcloud secrets create "$name" --data-file=-
done
```

Let Cloud Run's runtime account read them:

```bash
PROJECT_NUMBER=$(gcloud projects describe "$(gcloud config get-value project)" --format='value(projectNumber)')
for name in cartcompare-db cartcompare-jwt-key kroger-client-id kroger-client-secret; do
  gcloud secrets add-iam-policy-binding "$name" \
    --member="serviceAccount:${PROJECT_NUMBER}-compute@developer.gserviceaccount.com" \
    --role=roles/secretmanager.secretAccessor
done
```

## 4. API (Cloud Run)

Cloud Shell deploys what is on GitHub, so push `main` first. Pick the Vercel
project name now: the frontend will live at
`https://<vercel-project>.vercel.app`, which the API must allow (CORS).

```bash
git clone https://github.com/katienoetzel/CartCompare.git && cd CartCompare
gcloud run deploy cartcompare-api \
  --source backend \
  --region us-east1 \
  --allow-unauthenticated \
  --max-instances 1 \
  --memory 512Mi \
  --set-secrets "ConnectionStrings__DefaultConnection=cartcompare-db:latest,Jwt__Key=cartcompare-jwt-key:latest,Kroger__ClientId=kroger-client-id:latest,Kroger__ClientSecret=kroger-client-secret:latest" \
  --set-env-vars "Admin__Email=<your admin email>,Frontend__Origin=https://<vercel-project>.vercel.app,ForwardedHeaders__Enabled=true,Database__ApplyMigrations=true"
```

Why these settings:

| Setting | Reason |
| --- | --- |
| `--max-instances 1` | Stays inside the free tier, and rate limits are counted per instance. |
| `ForwardedHeaders__Enabled=true` | Cloud Run's proxy hides the client's IP; without this, every visitor shares one login limit. Never set it when the API is not behind a proxy. |
| `Database__ApplyMigrations=true` | Applies EF migrations on startup. Safe to leave on. |
| `Frontend__Origin` | Required outside Development; comma-separate several origins. |

The deploy prints the service URL (`https://cartcompare-api-....run.app`).
Check it: `curl <service-url>/api/retailers` returns `[]`.

## 4b. Fixed outgoing address (needed for Kroger and Harris Teeter)

Kroger's API sits behind Akamai, which answers requests from Cloud Run's
shared outgoing addresses with a 403 "Access Denied" page, even though the
token request succeeds. Requests from a regular Google VM address (as Cloud
Shell uses) are accepted. Route the service's outgoing traffic through a
reserved address with Direct VPC egress and Cloud NAT:

```bash
gcloud services enable compute.googleapis.com
gcloud compute addresses create cartcompare-egress --region=us-east1
gcloud compute routers create cartcompare-router --network=default --region=us-east1
gcloud compute routers nats create cartcompare-nat --router=cartcompare-router --region=us-east1 \
  --nat-all-subnet-ip-ranges --nat-external-ip-pool=cartcompare-egress
gcloud run services update cartcompare-api --region=us-east1 \
  --network=default --subnet=default --vpc-egress=all-traffic \
  --update-env-vars DOTNET_SYSTEM_NET_DISABLEIPV6=1
```

`DOTNET_SYSTEM_NET_DISABLEIPV6=1` matters: the VPC subnet only routes IPv4,
but Neon's hostname also resolves to IPv6. Without it, .NET sometimes tries
the IPv6 address first and startup crashes with `Failed to connect to
[2600:…]:5432` / `Network is unreachable`.

This needs the project's `default` network with a `us-east1` subnet
(`gcloud compute networks subnets list --network=default --regions=us-east1`).
The reserved address and NAT cost about $4–5 a month. To remove them:

```bash
gcloud run services update cartcompare-api --region=us-east1 --clear-network
gcloud compute routers nats delete cartcompare-nat --router=cartcompare-router --region=us-east1 --quiet
gcloud compute routers delete cartcompare-router --region=us-east1 --quiet
gcloud compute addresses delete cartcompare-egress --region=us-east1 --quiet
```

If Kroger or Harris Teeter fail again, the logs show Kroger's reply:
`gcloud logging read 'resource.labels.service_name="cartcompare-api" AND jsonPayload.LogLevel="Warning"' --freshness=1h --limit=1 --format='value(jsonPayload.Exception)'`.
An "Access Denied" page means the outgoing address is blocked.

Startup fails with a clear message if `Jwt__Key` is under 32 bytes or
`Frontend__Origin` is missing: see **Cloud Run → cartcompare-api → Logs**.
Logs are JSON; filter retailer calls with `jsonPayload.Category="CartCompare.Infrastructure.Providers.LoggingPriceProvider"`.

## 5. Frontend (Vercel)

1. **Add New → Project**, import the GitHub repo, and set **Root Directory**
   to `frontend`. Vercel detects Vite.
2. Add the environment variable `VITE_API_BASE_URL` = the Cloud Run service
   URL (no trailing slash).
3. Deploy. If the project URL differs from the `Frontend__Origin` you set,
   update the API:

   ```bash
   gcloud run services update cartcompare-api --region us-east1 --update-env-vars "Frontend__Origin=https://<actual>.vercel.app"
   ```

## 6. Admin user, retailers and starter items

1. On the live site, register with the `Admin__Email` address.
2. In Cloud Shell, get a token and create the retailers. The names must match
   exactly; the providers recognise them by name.

   ```bash
   API=<service-url>
   read -rsp "Admin password: " PASSWORD && echo
   TOKEN=$(curl -s "$API/api/auth/login" -H 'Content-Type: application/json' \
     -d "{\"email\":\"<your admin email>\",\"password\":\"$PASSWORD\"}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')
   for retailer in '{"name":"Kroger","supportsMembership":true}' '{"name":"Harris Teeter","supportsMembership":false}' '{"name":"Lowes Foods","supportsMembership":false}'; do
     curl -s "$API/api/retailers" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "$retailer"; echo
   done
   ```

3. Optionally add starter items the same way (`POST /api/items` with
   `name`, `size`, `category`), or let shoppers add them from store
   catalogs in the app. Items need a size to be matched automatically; use
   `per lb` for produce and meat sold by weight.

## 7. Smoke test

On the live site: register a second account, search ZIP 27613, add a few
items, select a Lowe's Foods and a Harris Teeter store, and compare. The first
comparison is slow (it looks up every product); a repeat within 6 hours reuses
prices and is fast.

## Updating

After pulling changes, redeploy with
`gcloud run deploy cartcompare-api --source backend --region us-east1`;
secrets, environment variables and the network settings from step 4b carry
over. Vercel redeploys the frontend on
every push to the main branch.
