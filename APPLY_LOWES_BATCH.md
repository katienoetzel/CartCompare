# CartCompare — Lowes Foods integration batch 1

This batch adds the first Lowes Foods/Inmar provider implementation.

## What is included

- `LowesFoodsSessionClient`
  - creates an isolated cookie-backed Inmar session per operation
  - initializes `/v2/session`
  - selects a store with `POST /v2/fulfillmentInfo`
  - searches with `GET /v2/search?searchTerms=...`
- `LowesFoodsPriceProvider`
  - implements `IPriceProvider`
  - maps Lowes Foods search results into CartCompare `ProviderProduct`
  - maps price/availability into `ProviderPriceSnapshot`
  - includes a first-pass locations parser
- `Program.cs`
  - registers the Lowes Foods provider alongside Kroger
- `IntegrationsController.cs`
  - adds Lowes Foods integration routes
  - adds a Development-only anonymous smoke-test endpoint

## Important

This is an integration spike against the browser-accessible Inmar interface used by
the Lowes Foods shopping site. The observed endpoints are not a documented public
developer API. The code intentionally keeps all session/cookie handling on the
CartCompare backend.

## Apply

Copy the `backend` folder in this ZIP over the existing repository root and allow
the included files to overwrite the matching files.

The new provider files should end up at:

`backend/src/CartCompare.Infrastructure/Providers/LowesFoods/`

## Verify

From:

`C:\Users\katel\git\CartCompare\backend`

run:

```powershell
dotnet build
```

If build succeeds, start the API:

```powershell
dotnet run --project src/CartCompare.Api/CartCompare.Api.csproj
```

In another PowerShell window, test the Lowes Foods store ID already observed in
DevTools:

```powershell
Invoke-RestMethod `
  "http://localhost:5055/api/integrations/lowes-foods/dev-search?locationId=4ZQtKNkUnZctHTeeZib5YE&query=whole%20milk" |
  ConvertTo-Json -Depth 6
```

Expected successful shape:

```json
[
  {
    "externalProductId": "38322",
    "name": "Lowes Foods Vitamin D Whole Milk 1 gal",
    "brand": null,
    "size": "128 oz",
    "upc": null
  }
]
```

There may be several search results; that is fine.

If the smoke test fails, send the complete terminal error/status output. Do not
send cookies, session IDs, or credentials.

## GitHub

Do not push this batch yet. First verify build + live Lowes Foods search locally.
