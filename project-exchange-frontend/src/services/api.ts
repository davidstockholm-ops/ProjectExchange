/**
 * Backend API client — Project Exchange Core.
 * Permanent default: http://localhost:5051 (drake-album backend). No env vars required.
 * Order: POST /api/secondary/order. Trade history: GET /api/secondary/trades/{marketId}.
 */
export const API_BASE = "http://localhost:5051";

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

/** GET /api/secondary/trades/{marketId} — fetch executed trades for a market. */
export async function getTradeHistory<T = unknown>(marketId: string): Promise<T> {
  const url = `${API_BASE}/api/secondary/trades/${encodeURIComponent(marketId.trim())}`;
  const res = await fetch(url, { headers: { "Content-Type": "application/json" } });
  if (!res.ok) throw new Error(`API error: ${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}

/** POST /api/secondary/order — submit an order (Swagger: query params). */
export async function postOrder(params: {
  marketId: string;
  price: number;
  quantity: number;
  side: "Buy" | "Sell";
  operatorId: string;
  userId: string;
}): Promise<unknown> {
  const search = new URLSearchParams({
    marketId: params.marketId,
    price: String(params.price),
    quantity: String(params.quantity),
    side: params.side,
    operatorId: params.operatorId,
    userId: params.userId,
  });
  const res = await fetch(`${API_BASE}/api/secondary/order?${search.toString()}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) throw new Error(`API error: ${res.status} ${res.statusText}`);
  return res.json();
}
