const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL;

if (!API_BASE_URL) {
  throw new Error(
    "VITE_API_BASE_URL is not configured."
  );
}

export async function apiRequest(
  path,
  options = {}
) {
  const token =
    sessionStorage.getItem(
      "cartcompare_token"
    );

  const headers =
    new Headers(options.headers || {});

  if (token) {
    headers.set(
      "Authorization",
      `Bearer ${token}`
    );
  }

  const response = await fetch(
    `${API_BASE_URL}${path}`,
    {
      ...options,
      headers,
    }
  );

  if (!response.ok) {
    let message =
      `API request failed with status ${response.status}`;

    try {
      const errorData =
        await response.json();

      if (errorData.message) {
        message =
          errorData.message;
      }

      if (
        Array.isArray(errorData.errors)
      ) {
        message =
          errorData.errors.join(", ");
      }
    } catch {
      // Response had no usable JSON body.
    }

    throw new Error(message);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}