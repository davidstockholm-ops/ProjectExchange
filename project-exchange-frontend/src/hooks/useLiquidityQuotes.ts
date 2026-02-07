"use client";

import { useEffect, useState } from "react";
import { getLiquidityQuotes, type AggregatedQuotesResponse } from "@/services/api";

export interface UseLiquidityQuotesResult {
  data: AggregatedQuotesResponse | null;
  error: Error | null;
  isLoading: boolean;
  refetch: () => void;
}

export function useLiquidityQuotes(marketId: string): UseLiquidityQuotesResult {
  const [data, setData] = useState<AggregatedQuotesResponse | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);

  const refetch = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!marketId.trim()) {
      setData(null);
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setError(null);
    setIsLoading(true);

    getLiquidityQuotes(marketId.trim())
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
  }, [marketId, refreshKey]);

  return { data, error, isLoading, refetch };
}
