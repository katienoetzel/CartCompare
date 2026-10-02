# Harris Teeter store discovery

Extract the backend folder over your repository root.

The existing Kroger API provider now supports the Harris Teeter retailer name, requests the Harris Teeter chain, and ignores locations labeled as another chain. A new integration test checks the query and mapping with stub HTTP responses.

Run `dotnet test` from backend. No database migration is needed.

After tests pass, create a Harris Teeter retailer through the existing admin POST /api/retailers endpoint if one does not already exist. The existing POST /api/store-locations/search then discovers its stores together with the other active retailers.
