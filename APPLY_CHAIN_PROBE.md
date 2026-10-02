# Development-only Kroger nearby-chain probe

Extract the backend folder over the repository root. Run dotnet test from backend, restart the API, then GET http://localhost:5055/api/dev/kroger/nearby-chains?postalCode=27613. It reports chain names and counts from the Kroger API without saving any stores. It returns 404 outside Development.
