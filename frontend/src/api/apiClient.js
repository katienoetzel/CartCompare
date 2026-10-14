const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL;

if (!API_BASE_URL) {
  throw new Error(
    "VITE_API_BASE_URL is not configured."
  );
}

const TOKEN_KEY = "cartcompare_token";

// Fired when a signed-in request is rejected because the token
// expired (tokens last an hour); the app returns to the login form.
export const SESSION_EXPIRED_EVENT =
  "cartcompare:session-expired";

export const SESSION_EXPIRED_MESSAGE =
  "Your session has expired. Please log in again.";

export async function apiRequest(
  path,
  options = {}
) {
  const token =
    sessionStorage.getItem(
      TOKEN_KEY
    );

  const headers =
    new Headers(options.headers || {});

  if (token) {
    headers.set(
      "Authorization",
      `Bearer ${token}`
    );
  }

  let response;

  try {
    response = await fetch(
      `${API_BASE_URL}${path}`,
      {
        ...options,
        headers,
      }
    );
  } catch {
    throw new Error(
      "Can't reach CartCompare right now. Check your connection and try again."
    );
  }

  if (!response.ok) {
    if (response.status === 401 && token) {
      sessionStorage.removeItem(TOKEN_KEY);
      window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
      throw new Error(SESSION_EXPIRED_MESSAGE);
    }

    throw new Error(
      await readErrorMessage(response)
    );
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

async function readErrorMessage(response) {
  try {
    const errorData =
      await response.json();

    if (errorData.message) {
      return errorData.message;
    }

    // Identity errors arrive as a list; model validation errors
    // as { field: [messages] }.
    if (Array.isArray(errorData.errors)) {
      return errorData.errors.join(", ");
    }

    if (errorData.errors && typeof errorData.errors === "object") {
      return Object.values(errorData.errors).flat().join(" ");
    }
  } catch {
    // Response had no usable JSON body.
  }

  if (response.status >= 500) {
    return "Something went wrong on our side. Please try again in a moment.";
  }

  return `Request failed (status ${response.status}).`;
}
