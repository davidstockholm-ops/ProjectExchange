/**
 * Backend API client and communication logic for Project Exchange Core.
 * Configure base URL and fetch wrappers here.
 */

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export async function apiFetch<T>(
  path: string,
  options?: RequestInit
): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });
  if (!res.ok) throw new Error(`API error: ${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}
