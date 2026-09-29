# CartCompare — Lowes Foods Batch 4

This batch fixes the session lifecycle based on the clean browser startup capture.

## What changed

The successful Lowes Foods browser flow showed that:

1. `POST /v2/session` happens first.
2. That request does **not** have `inmar-session-id`.
3. The POST body contains a client-generated `sessionId`.
4. The browser sends `inmar-banner-id: shop_lowes_foods`.
5. The browser sends an opaque `x-correlation-id`.
6. After the session exists, later requests use `inmar-session-id`.

CartCompare now follows that lifecycle:

```text
POST /v2/session
  body contains generated sessionId
      ↓
set inmar-session-id header
      ↓
POST /v2/configuration
      ↓
GET /v2/session
      ↓
POST /v2/fulfillmentInfo
      ↓
GET /v2/search
```

The provider generates its own session/correlation IDs. It does not use the
browser session ID that was shared during debugging.

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

Restart the backend:

```powershell
dotnet run --project src/CartCompare.Api/CartCompare.Api.csproj
```

Then in another PowerShell window:

```powershell
Invoke-RestMethod `
  "http://localhost:5055/api/integrations/lowes-foods/dev-search?locationId=4ZQtKNkUnZctHTeeZib5YE&query=whole%20milk" |
  ConvertTo-Json -Depth 6
```

If it succeeds, send the JSON. If it fails, send the first Lowes Foods exception
line plus the returned HTTP/body message; the full ASP.NET stack trace is no
longer necessary unless requested.
