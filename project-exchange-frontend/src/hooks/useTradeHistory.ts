"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/services/api";

export type TradeSide = "Buy" | "Sell";

export interface TradeRecord {
  id: string;
  time: string; // ISO 8601
  price: number;
  size: number;
  side: TradeSide;
}

export interface TradeHistoryData {
  outcomeId: string;
  trades: TradeRecord[];
}

/** High-quality mock trade history for when API is unavailable. */
function getMockTradeHistory(outcomeId: string): TradeHistoryData {
  const now = Date.now();
  const baseTime = (ms: number) => new Date(now - ms).toISOString();

  return {
    outcomeId,
    trades: [
      { id: "t1", time: baseTime(12_000), price: 0.67, size: 45, side: "Sell" },
      { id: "t2", time: baseTime(28_000), price: 0.66, size: 120, side: "Buy" },
      { id: "t3", time: baseTime(45_000), price: 0.68, size: 80, side: "Sell" },
      { id: "t4", time: baseTime(67_000), price: 0.65, size: 200, side: "Buy" },
      { id: "t5", time: baseTime(92_000), price: 0.69, size: 55, side: "Sell" },
      { id: "t6", time: baseTime(115_000), price: 0.64, size: 90, side: "Buy" },
      { id: "t7", time: baseTime(140_000), price: 0.67, size: 110, side: "Buy" },
      { id: "t8", time: baseTime(168_000), price: 0.70, size: 35, side: "Sell" },
      { id: "t9", time: baseTime(195_000), price: 0.63, size: 165, side: "Buy" },
      { id: "t10", time: baseTime(220_000), price: 0.71, size: 42, side: "Sell" },
    ],
  };
}

/** Normalize backend response (camelCase or PascalCase). */
function mapResponse(raw: {
  outcomeId?: string;
  OutcomeId?: string;
  trades?: Array<{
    id?: string;
    Id?: string;
    time?: string;
    Time?: string;
    price?: number;
    Price?: number;
    size?: number;
    Size?: number;
    side?: string;
    Side?: string;
  }>;
}): TradeHistoryData {
  const outcomeId = (raw.outcomeId ?? raw.OutcomeId ?? "").toString();
  const rawTrades = raw.trades ?? [];
  const trades: TradeRecord[] = rawTrades.map((t, i) => {
    const side = (t.side ?? t.Side ?? "Buy").toString();
    return {
      id: (t.id ?? t.Id ?? `trade-${i}`).toString(),
      time: (t.time ?? t.Time ?? new Date().toISOString()).toString(),
      price: Number(t.price ?? t.Price ?? 0),
      size: Number(t.size ?? t.Size ?? 0),
      side: side === "Sell" ? "Sell" : "Buy",
    };
  });
  return { outcomeId, trades };
}

export interface UseTradeHistoryResult {
  data: TradeHistoryData | null;
  error: Error | null;
  isLoading: boolean;
  isMock: boolean;
}

/**
 * Fetches trade history from GET /api/markets/trades/{outcomeId}.
 * On API failure, returns high-quality mock data for UI development.
 */
export function useTradeHistory(outcomeId: string): UseTradeHistoryResult {
  const [data, setData] = useState<TradeHistoryData | null>(null);
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

    apiFetch<{ outcomeId?: string; OutcomeId?: string; trades?: unknown[] }>(
      `/api/markets/trades/${encodeURIComponent(outcomeId.trim())}`
    )
      .then((raw) => {
        if (cancelled) return;
        setData(mapResponse(raw as Parameters<typeof mapResponse>[0]));
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err instanceof Error ? err : new Error(String(err)));
        setData(getMockTradeHistory(outcomeId.trim()));
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
