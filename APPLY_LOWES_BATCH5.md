# CartCompare — Lowes Foods Batch 5

This is the first end-to-end database/pipeline smoke test.

It does NOT replace the working provider. It adds a Development-only endpoint
that proves the Lowes Foods provider can run through CartCompare's normal
database + product-price sync pipeline.

The endpoint:

1. Ensures the `Lowes Foods` Retailer exists locally.
2. Ensures the known Garner store exists locally.
3. Ensures the canonical `Whole Milk / 1 gallon` Item exists.
4. Searches the live Lowes Foods catalog.
5. Selects product `38322` when available.
6. Calls the existing `IProductPriceSyncService`.
7. Returns the RetailerProduct + Price data persisted to PostgreSQL.

The endpoint returns 404 outside Development.

## Apply

Extract into:

`C:\Users\katel\git\CartCompare`

No existing file should need to be overwritten; this batch adds one controller.

## Verify

From:

`C:\Users\katel\git\CartCompare\backend`

run:

```powershell
dotnet build
```

Restart the backend:

```powershell
dotnet run --project src/CartCompare.Api/CartCompare.Api.csproj
```

Then in another PowerShell window run:

```powershell
Invoke-RestMethod `
  -Method POST `
  "http://localhost:5055/api/dev/lowes-foods/bootstrap-and-sync" |
  ConvertTo-Json -Depth 8
```

Success should show approximately:

- retailer.name = `Lowes Foods`
- candidate.externalProductId = `38322`
- sync.result = `Succeeded`
- sync.priceSynced = `true`
- persisted.regularPrice = a real Lowes Foods price
- persisted.sourceProvider = `LowesFoodsInmar`

If it fails, send the returned JSON or the first meaningful exception message.
