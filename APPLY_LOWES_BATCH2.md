# CartCompare — Lowes Foods Batch 2

This fixes the first live Inmar error from Batch 1:

`request/headers must have required property 'inmar-session-id'`

CartCompare now sends a fresh opaque URL-safe session identifier on each isolated
Lowes Foods provider operation.

## Apply

Extract/copy this ZIP into:

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

If it fails, send the new backend exception.
