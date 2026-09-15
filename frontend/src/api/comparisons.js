import { apiRequest } from "./apiClient";

export function compareStores(
  storeLocationIds
) {
  return apiRequest(
    "/api/comparisons/stores",
    {
      method: "POST",
      headers: {
        "Content-Type":
          "application/json",
      },
      body: JSON.stringify({
        storeLocationIds,
      }),
    }
  );
}