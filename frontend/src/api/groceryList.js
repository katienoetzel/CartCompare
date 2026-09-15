import { apiRequest } from "./apiClient";

export function getGroceryList() {
  return apiRequest(
    "/api/grocery-list"
  );
}

export function setGroceryListQuantity(
  itemId,
  quantity
) {
  return apiRequest(
    `/api/grocery-list/${itemId}`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        quantity,
      }),
    }
  );
}

export function removeGroceryListItem(
  itemId
) {
  return apiRequest(
    `/api/grocery-list/${itemId}`,
    {
      method: "DELETE",
    }
  );
}