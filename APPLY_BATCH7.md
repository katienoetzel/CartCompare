# Batch 7: customer-facing nearby store search

Copy the backend folder from this ZIP over the root of your CartCompare checkout.

New authenticated endpoint: POST /api/store-locations/search
Body: {"postalCode":"27613"}
The response contains postalCode, stores (with persisted IDs and retailer names), and failedRetailers. The request searches each active supported retailer and refreshes matching store records. It only returns locations from the current search; old saved locations are excluded.

From C:\Users\katel\git\CartCompare\backend run: dotnet test
After tests pass, restart the API. No database migration is needed.
