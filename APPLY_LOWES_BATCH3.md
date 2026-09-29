# CartCompare — Lowes Foods Batch 3

This batch reproduces the browser bootstrap sequence discovered in DevTools.

Observed sequence:

1. `POST /v2/configuration`
2. `GET /v2/session`
3. `POST /v2/fulfillmentInfo`
4. `GET /v2/search?searchTerms=...`

The configuration request uses the same `inmar-session-id` header and sends:

```json
{
  "configurationKeyPrefixes": [
    "apps/consumer/raven/logs/",
    "providers/authentication/",
    "ice/",
    "apps/consumer/raven/analytics/"
  ]
}
```

## Apply

Extract this ZIP into:

`C:\Users\katel\git\CartCompare`

Allow the included file to overwrite the existing copy.

## Verify

From:

`C:\Users\katel\git\CartCompare\backend`

run:

```powershell
dotnet build
```

Then restart the backend:

```powershell
dotnet run --project src/CartCompare.Api/CartCompare.Api.csproj
```

In another PowerShell window:

```powershell
Invoke-RestMethod `
  "http://localhost:5055/api/integrations/lowes-foods/dev-search?locationId=4ZQtKNkUnZctHTeeZib5YE&query=whole%20milk" |
  ConvertTo-Json -Depth 6
```

If it fails, send the new backend exception. If it succeeds, send the JSON output.
