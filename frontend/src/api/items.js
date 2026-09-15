import { apiRequest } from "./apiClient";

export function getItems() {
  return apiRequest("/api/items");
}