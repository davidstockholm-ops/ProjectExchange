"use client";

import { useCallback, useEffect, useState } from "react";
import { getPosition, type NetPositionDto, type PositionResponse } from "@/services/api";

export interface UsePositionResult {
  data: PositionResponse | null;
  error: Error | null;
  isLoading: boolean;
  refetch: () => void;
}

/**
 * Fetches net position for a user from GET /api/portfolio/position.
 * Call refetch when SignalR TradeMatched is received for this user to update in real time.
 */
export function usePosition(userId: string, marketId?: string): UsePositionResult {
  const [data, setData] = useState<PositionResponse | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);

  const refetch = useCallback(() => setRefreshKey((k) => k + 1), []);

  useEffect(() => {
    if (!userId.trim()) {
      setData(null);
      setError(new Error("userId is required"));
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setError(null);
    setIsLoading(true);

    getPosition(userId.trim(), marketId?.trim())
      .then((res) => {
        if (!cancelled) setData(res);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err : new Error(String(err)));
          setData(null);
        }
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [userId, marketId ?? "", refreshKey]);

  return { data, error, isLoading, refetch };
}
