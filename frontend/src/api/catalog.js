import { apiRequest } from "./apiClient";

export function searchCatalog(storeLocationId, query) {
  const params = new URLSearchParams({
    storeLocationId: String(storeLocationId),
    query,
  });

  return apiRequest(`/api/catalog/products?${params}`);
}

export function createCatalogItem(request) {
  return apiRequest("/api/catalog/items", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}
