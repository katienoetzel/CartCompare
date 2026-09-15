import { apiRequest } from "./apiClient";

export function getRetailers() {
  return apiRequest("/api/retailers");
}