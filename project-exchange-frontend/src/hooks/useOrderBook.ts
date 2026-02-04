"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/services/api";

export interface OrderBookLevel {
  orderId: string;
  userId: string;
  price: number;
  quantity: number;
}

export interface OrderBookData {
  outcomeId: string;
  bids: OrderBookLevel[];
  asks: OrderBookLevel[];
}

/** High-quality mock order book so depth effect is visible when API is unavailable. */
function getMockOrderBook(outcomeId: string): OrderBookData {
  return {
    outcomeId,
    asks: [
      { orderId: "a1", userId: "seller-1", price: 0.72, quantity: 120 },
      { orderId: "a2", userId: "seller-2", price: 0.71, quantity: 85 },
      { orderId: "a3", userId: "seller-3", price: 0.70, quantity: 200 },
      { orderId: "a4", userId: "seller-4", price: 0.69, quantity: 45 },
      { orderId: "a5", userId: "seller-5", price: 0.68, quantity: 310 },
      { orderId: "a6", userId: "seller-6", price: 0.67, quantity: 90 },
      { orderId: "a7", userId: "seller-7", price: 0.66, quantity: 165 },
    ],
    bids: [
      { orderId: "b1", userId: "buyer-1", price: 0.65, quantity: 280 },
      { orderId: "b2", userId: "buyer-2", price: 0.64, quantity: 110 },
      { orderId: "b3", userId: "buyer-3", price: 0.63, quantity: 195 },
      { orderId: "b4", userId: "buyer-4", price: 0.62, quantity: 70 },
      { orderId: "b5", userId: "buyer-5", price: 0.61, quantity: 240 },
      { orderId: "b6", userId: "buyer-6", price: 0.60, quantity: 55 },
      { orderId: "b7", userId: "buyer-7", price: 0.59, quantity: 130 },
    ],
  };
}

/** Backend returns camelCase in JSON; C# uses PascalCase — normalize to our shape. */
function mapResponse(raw: {
  outcomeId: string;
  bids: Array<{ orderId?: string; OrderId?: string; userId?: string; UserId?: string; price?: number; Price?: number; quantity?: number; Quantity?: number }>;
  asks: Array<{ orderId?: string; OrderId?: string; userId?: string; UserId?: string; price?: number; Price?: number; quantity?: number; Quantity?: number }>;
}): OrderBookData {
  const mapLevel = (l: (typeof raw.bids)[0]): OrderBookLevel => ({
    orderId: (l.orderId ?? l.OrderId ?? "").toString(),
    userId: (l.userId ?? l.UserId ?? "").toString(),
    price: Number(l.price ?? l.Price ?? 0),
    quantity: Number(l.quantity ?? l.Quantity ?? 0),
  });
  return {
    outcomeId: raw.outcomeId,
    bids: (raw.bids ?? []).map(mapLevel),
    asks: (raw.asks ?? []).map(mapLevel),
  };
}

export interface UseOrderBookResult {
  data: OrderBookData | null;
  error: Error | null;
  isLoading: boolean;
  isMock: boolean;
}

/**
 * Fetches order book for an outcome from GET /api/markets/orderbook/{outcomeId}.
 * On API failure, returns high-quality mock data so the depth effect is visible in the UI.
 */
export function useOrderBook(outcomeId: string): UseOrderBookResult {
  const [data, setData] = useState<OrderBookData | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isMock, setIsMock] = useState(false);

  useEffect(() => {
    if (!outcomeId.trim()) {
      setData(null);
      setError(new Error("OutcomeId is required"));
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setError(null);
    setIsLoading(true);
    setIsMock(false);

    apiFetch<{
      outcomeId: string;
      bids: unknown[];
      asks: unknown[];
    }>(`/api/markets/orderbook/${encodeURIComponent(outcomeId.trim())}`)
      .then((raw) => {
        if (cancelled) return;
        setData(mapResponse(raw as Parameters<typeof mapResponse>[0]));
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err instanceof Error ? err : new Error(String(err)));
        setData(getMockOrderBook(outcomeId.trim()));
        setIsMock(true);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [outcomeId]);

  return { data, error, isLoading, isMock };
}
