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
- `.github/workflows/ci.yml`: GitHub Actions CI

## Commands

- Backend unit tests: `dotnet test backend/tests/CartCompare.Services.Tests`
- Backend integration tests: `dotnet test backend/tests/CartCompare.IntegrationTests`
  - Needs user secret `ConnectionStrings:TestConnection` on the IntegrationTests project, pointing to a **separate** database (e.g. `cartcompare_test`). The fixture drops and recreates the `public` schema, so never point it at the dev database.
- Frontend: `npm run dev`, `npm test`, `npm run lint`, `npm run e2e` (from `frontend/`)
- Local secrets (API project): `Jwt:Key`, `ConnectionStrings:DefaultConnection` via .NET user secrets.

## Domain model

- Canonical `Item` (what the user wants, e.g. "Whole Milk", 1 gallon) is separate from `RetailerProduct` (a specific retailer's product). Several retailer products can map to one item.
- Prices and availability are stored per physical `StoreLocation`: regular, sale and membership prices.
- Also: users, retailers, grocery lists, saved items, user retailer memberships.
- Catalog management and manual sync endpoints require the administrator policy.

## Retailers

- Kroger Developer API: store discovery, product search, pricing.
- Harris Teeter: through the Kroger integration, chain id `HART`; locations from other chains are ignored.
- Lowe's Foods: Inmar shopping service for locations, search and pricing. Saved product IDs are recovered after an API restart by searching the saved product name.
- Store sync uses external location IDs, so it updates existing records instead of creating duplicates.
- If one retailer fails, results from the others are still returned.

## Comparison and matching rules

- Comparisons apply list quantities and the user's membership status. Complete stores are ranked by total; incomplete stores and missing prices are reported separately, never as a complete basket total.
- Comparison-triggered sync: prices checked within 6 hours are reused; missing or older ones are refreshed. Stale prices are excluded from rankings when refresh fails.
- Outcomes are distinguished: uncertain match, unavailable product, missing price, provider failure.
- `ProductMatchingService` (deterministic only):
  - A UPC matching an existing retailer product for the item is the strongest match.
  - An item **with** a brand only matches that brand. An item with **no** brand is generic and matches any brand.
  - If the item has a size, it must match after normalization ("1 gallon" = "1 gal").
  - Names match exactly, or all of the item's (at least 2) meaningful words appear in the product name.
  - Variant guard: a containment match is rejected if the product adds a variant word the item doesn't have (chocolate, lactose, organic, reduced, ultra, …) or a different fat percentage.
  - "Vitamin D Milk" is treated as whole milk (that's how Harris Teeter names it).

## Working conventions

- Explain changes clearly and keep them focused.
- Code style: verbose, one-argument-per-line formatting with explanatory comments, as in the existing services. Match it.
- Make changes directly in the repo; don't produce ZIPs or "copy this folder over" instructions.
- `bin/`, `obj/`, `coverage/` and `test-results/` are gitignored and must not be committed.

## Status (as of 2026-10-13)

The full plan and to-do list live in the shared roadmap doc: https://claude.ai/code/artifact/72189f0b-4292-43b2-a88d-6d3360bd5385 (tick tasks there as they're verified).

Working locally: auth, grocery lists, ZIP store discovery (Kroger, Harris Teeter, Lowe's Foods), store picker, comparisons, Lowe's Foods live pricing (Whole Milk $2.99 verified), automatic matching and price sync.

Next steps, in order:
1. Verify generic matching live: clear the "Kroger" brand on the Whole Milk item (no item update endpoint exists yet), then confirm Harris Teeter and Lowe's Foods both produce usable comparisons.
2. Test a realistic basket: several items, package sizes, unavailable products, membership prices, repeated comparisons.
3. Frontend flow for resolving uncertain matches (users can only see an explanation today).
4. Finish/verify saved-item and membership frontend workflows.
5. Production readiness: limits on costly comparison requests, configuration, logging, failure handling.
6. Deploy: Railway (API + DB), Vercel (frontend). Not yet done.

Later: background price refresh, more retailers.
