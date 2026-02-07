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

// --- Portfolio positions (net from TradeMatched events) ---

export interface NetPositionDto {
  outcomeId: string;
  netQuantity: number;
}

export interface PositionResponse {
  userId: string;
  positions: NetPositionDto[];
}

export async function getPosition(
  userId: string,
  marketId?: string
): Promise<PositionResponse> {
  const params = new URLSearchParams({ userId });
  if (marketId?.trim()) params.set("marketId", marketId.trim());
  const res = await fetch(`${API_BASE}/api/portfolio/position?${params.toString()}`, {
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) throw new Error(`API error: ${res.status} ${res.statusText}`);
  return res.json();
}

// --- Liquidity: aggregated quotes and settings ---

export interface QuoteLevel {
  price: number;
  quantity: number;
}

export interface LiquidityQuoteResult {
  marketId: string;
  providerId: string;
  bestBid: number | null;
  bestAsk: number | null;
  spread: number | null;
  bids: QuoteLevel[];
  asks: QuoteLevel[];
}

export interface AggregatedQuotesResponse {
  marketId: string;
  providerQuotes: LiquidityQuoteResult[];
}

export async function getLiquidityQuotes(marketId: string): Promise<AggregatedQuotesResponse> {
  const res = await fetch(
    `${API_BASE}/api/liquidity/quotes?marketId=${encodeURIComponent(marketId.trim())}`,
    { headers: { "Content-Type": "application/json" } }
  );
  if (!res.ok) throw new Error(`API error: ${res.status} ${res.statusText}`);
  return res.json();
}

export interface LiquiditySettingsResponse {
  configEnabledProviders: string[];
  runtimeOverrideProviders: string[] | null;
  effectiveEnabled: string[];
  restrictedMarkets: string[];
}

export async function getLiquiditySettings(): Promise<LiquiditySettingsResponse> {
  const res = await fetch(`${API_BASE}/api/liquidity/settings`, {
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) throw new Error(`API error: ${res.status} ${res.statusText}`);
  return res.json();
}

export async function patchLiquiditySettings(enabledProviders: string[] | null): Promise<LiquiditySettingsResponse> {
  const res = await fetch(`${API_BASE}/api/liquidity/settings`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ enabledProviders }),
  });
  if (!res.ok) throw new Error(`API error: ${res.status} ${res.statusText}`);
  return res.json();
}
