import { apiRequest } from "./apiClient";

export function getStoresByRetailer(
  retailerId
) {
  return apiRequest(
    `/api/store-locations/retailer/${retailerId}`
  );
}