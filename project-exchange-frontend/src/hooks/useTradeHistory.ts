"use client";

import { useCallback, useEffect, useState } from "react";
import { getTradeHistory } from "@/services/api";

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

/** Map GET http://localhost:5051/api/secondary/trades/{marketId} response. */
function mapResponse(raw: {
  marketId?: string;
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
    quantity?: number;
    Quantity?: number;
    side?: string;
    Side?: string;
  }>;
}): TradeHistoryData {
  const outcomeId = (raw.marketId ?? raw.outcomeId ?? raw.OutcomeId ?? "").toString();
  const rawTrades = raw.trades ?? [];
  const trades: TradeRecord[] = rawTrades.map((t, i) => {
    const side = (t.side ?? t.Side ?? "Buy").toString();
    return {
      id: (t.id ?? t.Id ?? `trade-${i}`).toString(),
      time: (t.time ?? t.Time ?? new Date().toISOString()).toString(),
      price: Number(t.price ?? t.Price ?? 0),
      size: Number(t.size ?? t.Size ?? t.quantity ?? t.Quantity ?? 0),
      side: side === "Sell" ? "Sell" : "Buy",
    };
  });
  return { outcomeId, trades };
}

export interface UseTradeHistoryResult {
  data: TradeHistoryData | null;
  error: Error | null;
  isLoading: boolean;
  /** Append a live trade (e.g. from SignalR) to the list. */
  appendTrade: (trade: TradeRecord) => void;
}

/**
 * Fetches trade history from GET http://localhost:5051/api/secondary/trades/{marketId}.
 * Real backend only; no mock. On failure shows empty list and error (visible on dashboard).
 */
export function useTradeHistory(outcomeId: string): UseTradeHistoryResult {
  const [data, setData] = useState<TradeHistoryData | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(true);

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

    getTradeHistory<{ marketId?: string; outcomeId?: string; trades?: unknown[] }>(outcomeId.trim())
      .then((raw) => {
        if (cancelled) return;
        setData(mapResponse(raw as Parameters<typeof mapResponse>[0]));
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err instanceof Error ? err : new Error(String(err)));
        setData({ outcomeId: outcomeId.trim(), trades: [] });
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [outcomeId]);

  const appendTrade = useCallback(
    (trade: TradeRecord) => {
      setData((prev) =>
        prev
          ? { ...prev, trades: [trade, ...prev.trades] }
          : { outcomeId: outcomeId.trim(), trades: [trade] }
      );
    },
    [outcomeId]
  );

  return { data, error, isLoading, appendTrade };
}
