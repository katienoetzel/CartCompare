import { apiRequest } from "./apiClient";

export function searchNearbyStores(postalCode) {
  return apiRequest("/api/store-locations/search", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ postalCode }),
  });
}
