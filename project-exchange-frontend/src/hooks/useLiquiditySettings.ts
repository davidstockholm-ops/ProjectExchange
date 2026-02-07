"use client";

import { useCallback, useEffect, useState } from "react";
import {
  getLiquiditySettings,
  patchLiquiditySettings,
  type LiquiditySettingsResponse,
} from "@/services/api";

export interface UseLiquiditySettingsResult {
  data: LiquiditySettingsResponse | null;
  error: Error | null;
  isLoading: boolean;
  refetch: () => void;
  setEnabledProviders: (providerIds: string[] | null) => Promise<void>;
  toggleProvider: (providerId: string) => Promise<void>;
}

export function useLiquiditySettings(): UseLiquiditySettingsResult {
  const [data, setData] = useState<LiquiditySettingsResponse | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);

  const refetch = useCallback(() => setRefreshKey((k) => k + 1), []);

  useEffect(() => {
    let cancelled = false;
    setError(null);
    setIsLoading(true);

    getLiquiditySettings()
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
  }, [refreshKey]);

  const setEnabledProviders = useCallback(async (providerIds: string[] | null) => {
    await patchLiquiditySettings(providerIds);
    setRefreshKey((k) => k + 1);
  }, []);

  const toggleProvider = useCallback(
    async (providerId: string) => {
      const current = data?.effectiveEnabled ?? [];
      const isEnabled = current.some(
        (p) => p.toLowerCase() === providerId.toLowerCase()
      );
      const next = isEnabled
        ? current.filter((p) => p.toLowerCase() !== providerId.toLowerCase())
        : [...current, providerId];
      await patchLiquiditySettings(next.length ? next : null);
      setRefreshKey((k) => k + 1);
    },
    [data?.effectiveEnabled]
  );

  return {
    data,
    error,
    isLoading,
    refetch,
    setEnabledProviders,
    toggleProvider,
  };
}
