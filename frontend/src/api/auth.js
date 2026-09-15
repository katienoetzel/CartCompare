import { apiRequest } from "./apiClient";

const TOKEN_KEY = "cartcompare_token";

export async function login(email, password) {
  const data = await apiRequest("/api/auth/login", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      email,
      password,
    }),
  });

  const token =
    data.token ??
    data.accessToken;

  if (!token) {
    throw new Error(
      "Login succeeded, but no JWT was returned."
    );
  }

  sessionStorage.setItem(
    TOKEN_KEY,
    token
  );

  return data;
}

export async function register(
  firstName,
  lastName,
  email,
  password
) {
  return apiRequest("/api/auth/register", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      firstName,
      lastName,
      email,
      password,
    }),
  });
}

export function logout() {
  sessionStorage.removeItem(
    TOKEN_KEY
  );
}

export function getToken() {
  return sessionStorage.getItem(
    TOKEN_KEY
  );
}

export function isLoggedIn() {
  return getToken() !== null;
}