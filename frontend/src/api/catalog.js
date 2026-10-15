import { apiRequest } from "./apiClient";

export function searchCatalog(storeLocationId, query) {
  const params = new URLSearchParams({
    storeLocationId: String(storeLocationId),
    query,
  });

  return apiRequest(`/api/catalog/products?${params}`);
}

// Existing items a picked product already counts as, and the new
// items ("Branded" / "Generic") the server can build from it.
export function getProductOptions(storeLocationId, externalProductId) {
  const params = new URLSearchParams({
    storeLocationId: String(storeLocationId),
  });

  return apiRequest(
    `/api/catalog/products/${encodeURIComponent(externalProductId)}/options?${params}`
  );
}

export function createCatalogItem(storeLocationId, externalProductId, kind) {
  return apiRequest("/api/catalog/items", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ storeLocationId, externalProductId, kind }),
  });
}
