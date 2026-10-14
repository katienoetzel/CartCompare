# CartCompare

Full-stack grocery price comparison app. Users build a grocery list, find nearby physical stores by ZIP code, and compare the cost of their list across selected store locations.

Stack: ASP.NET Core (.NET 10), EF Core, PostgreSQL, ASP.NET Core Identity + JWT, React 19 + Vite (JavaScript).

## Layout

- `backend/CartCompare.slnx`: solution
  - `src/CartCompare.Api`: controllers, request/response models, `Program.cs` (runs on http://localhost:5055)
  - `src/CartCompare.Services`: business logic (comparisons, matching, price sync)
  - `src/CartCompare.Repositories`: data access
  - `src/CartCompare.Infrastructure`: `CartCompareDbContext`, migrations, external retailer providers (`Providers/Kroger`, `Providers/LowesFoods`)
  - `src/CartCompare.Providers`: shared price-provider interface and models (`ProviderProduct`)
  - `src/CartCompare.Entities`: EF entities
  - `tests/CartCompare.Services.Tests`: xUnit + Moq unit tests
  - `tests/CartCompare.IntegrationTests`: API, auth, repository and PostgreSQL tests
- `frontend/`: React app; Vitest + React Testing Library + MSW; Playwright in `e2e/`
- `.github/workflows/ci.yml`: GitHub Actions CI (backend tests, frontend tests, Playwright E2E)
- `docs/DEPLOYMENT.md`: step-by-step production deployment

## Commands

- Backend unit tests: `dotnet test backend/tests/CartCompare.Services.Tests`
- Backend integration tests: `dotnet test backend/tests/CartCompare.IntegrationTests`
  - Needs user secret `ConnectionStrings:TestConnection` on the IntegrationTests project, pointing to a **separate** database (e.g. `cartcompare_test`). The fixture drops and recreates the `public` schema, so never point it at the dev database.
- Frontend: `npm run dev`, `npm test`, `npm run lint`, `npm run e2e` (from `frontend/`)
  - E2E starts its own API (port 5055) and Vite server, so stop running ones first. The API runs with `E2E__Enabled` (resets `cartcompare_e2e`, from user secret `ConnectionStrings:E2EConnection`) and `Admin__Email=e2e-admin@example.com`, the account the comparison test uses for admin-only setup.
- CI results: `gh run list --branch main`, `gh run view <id> --log-failed` (the GitHub CLI is signed in).
- Local secrets (API project): `Jwt:Key`, `ConnectionStrings:DefaultConnection`, `Admin:Email`, Kroger credentials via .NET user secrets.

## Domain model

- Canonical `Item` (what the user wants, e.g. "Whole Milk", 1 gallon) is separate from `RetailerProduct` (a specific retailer's product). Several retailer products can map to one item.
- Prices and availability are stored per physical `StoreLocation`: regular, sale and membership prices.
- An item sized `per lb` (Bananas, Chicken Breast) is bought by the pound: the list quantity is pounds and prices are per pound.
- Also: users, retailers, grocery lists (max 50 items), saved items, user retailer memberships.
- Catalog management and manual sync endpoints require the administrator policy (`Admin:Email`). Admins edit items with `PUT /api/items/{id}`.
- Shoppers add missing items through `api/catalog`: they pick a real product from a nearby store, and the item's (editable) name and size must still match that product. An existing item with the same name and size is reused.

## Retailers

- Kroger Developer API: store discovery, product search, pricing.
- Harris Teeter: through the Kroger integration, chain id `HART`; locations from other chains are ignored.
- Lowe's Foods: Inmar shopping service for locations, search and pricing; no credentials needed. Products carry no brand. Saved product IDs are recovered after an API restart by searching the saved product name.
- Retailer names must be exactly `Kroger`, `Harris Teeter` and `Lowes Foods`; providers are chosen by name.
- Providers report products sold by weight with size `per lb` and a per-pound price, and give each search result a `ShelfPrice` (used only to order candidates).
- Every provider call is logged by `LoggingPriceProvider` (provider, operation, store, outcome, duration; no postal codes).
- Store sync uses external location IDs, so it updates existing records instead of creating duplicates.
- If one retailer fails, results from the others are still returned.

## Comparison and matching rules

- Comparisons apply list quantities and the user's membership status. Complete stores are ranked by total; incomplete stores and missing prices are reported separately, never as a complete basket total. At most 10 stores per comparison.
- Each item at each store is priced at its cheapest usable linked product. For a `per lb` item, a fixed-weight package is converted to a per-pound price.
- Comparison-triggered sync: prices checked within 6 hours are reused; missing or older ones are refreshed. Stale prices are excluded from rankings when refresh fails.
- Discovery (an item with no product at a retailer yet): search the store, keep the candidates that pass `AutomaticProductMatchPolicy`, and link the cheapest by `ShelfPrice`, trying up to 3 if a price is unusable.
- Outcomes are distinguished: uncertain match, unavailable product, missing price, provider failure.
- `ProductMatchingService` (deterministic only):
  - A UPC matching an existing retailer product for the item is the strongest match.
  - An item **with** a brand only matches that brand. An item with **no** brand is generic and matches any brand.
  - If the item has a size, it must match in any unit (`ProductSize`): 1 lb = 16 oz, 1 gallon = 128 fl oz, 1 dozen = 12 ct; "4 ct / 16 oz" matches on either size. A `per lb` item matches weighed products and any fixed-weight package.
  - Names match exactly, or all of the item's (at least 2) meaningful words appear in the product name. Plurals and punctuation such as "&" are ignored; extra words (brand, "Grade A", "Medium Roast") are allowed.
  - A one-word `per lb` item ("Bananas") matches only products whose name ends with that word, before any "(", "–" or "," ("Fresh Bunch of Bananas", not "Banana Squash").
  - Variant guard: a match is rejected if the product adds a variant word the item doesn't have (chocolate, lactose, organic, reduced, ultra, decaf, whipped, frozen, dried, breaded, whole grain, extra (large), jumbo, …) or a different fat percentage.
  - "Vitamin D Milk" is treated as whole milk (that's how Harris Teeter names it).
- `AutomaticProductMatchPolicy`: a product is linked without a person checking it only for a UPC match, or a deterministic match of 0.90+ confidence where both sides have a size.

## Production configuration

- Rate limits per user (or per IP before login), per minute: comparisons 6, store and catalog searches 20, login/register 10. Set via `RateLimits:*`.
- Outside Development: `Frontend:Origin` is required (comma-separated), `*DevelopmentController` classes are not routed, logs are JSON, HSTS is on.
- `Jwt:Key` must be at least 32 bytes. Tokens last 1 hour; the frontend returns to the login form when one expires.
- `ForwardedHeaders:Enabled=true` only behind a proxy (Cloud Run), so rate limits see real client IPs.
- `Database:ApplyMigrations=true` applies EF migrations on startup.

## Working conventions

- Explain changes clearly and keep them focused.
- Code style: verbose, one-argument-per-line formatting with explanatory comments, as in the existing services. Match it.
- Make changes directly in the repo; don't produce ZIPs or "copy this folder over" instructions.
- `bin/`, `obj/`, `coverage/` and `test-results/` are gitignored and must not be committed.

## Status (as of 2026-10-14)

The full plan and to-do list live in the shared roadmap doc: https://claude.ai/code/artifact/72189f0b-4292-43b2-a88d-6d3360bd5385 (tick tasks there as they're verified). Its "Open questions" set the order: launch as soon as possible, and ship phases 2 and 3 after the first live version.

Done and verified locally: the "Now" phase, the realistic basket test (both Lowe's Foods stores price all 10 staples; Harris Teeter's gaps are real stock differences), and production readiness. CI passes on `main`.

Deployed 2026-10-14 (see `docs/DEPLOYMENT.md`): frontend https://cart-compare-flame.vercel.app, API https://cartcompare-api-393971004541.us-east1.run.app (Google Cloud project `cartcompare-511622`, free trial until 2027-01-13), database on Neon. Kroger's Akamai firewall blocks Cloud Run's shared outgoing addresses (403 "Access Denied"), so the service sends traffic through Cloud NAT with a reserved address (`cartcompare-egress`, about $4–5 a month). Deploys run from Cloud Shell; the user has no local `gcloud` or Docker.

Next steps, in order:
1. Finish the production smoke test: register a non-admin user, ZIP 27613, build a list, compare a Lowe's Foods and a Harris Teeter store.
2. Portfolio polish: one link, a demo user, a real comparison in under a minute.
3. After launch: resolve uncertain matches in the UI (closest candidate shown with an "uncertain" badge, "choose another"), then saved-item and membership frontend workflows.

Later: background price refresh, more retailers.
