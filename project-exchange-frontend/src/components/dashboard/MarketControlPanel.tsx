"use client";

import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useLiquidityQuotes } from "@/hooks/useLiquidityQuotes";
import { useLiquiditySettings } from "@/hooks/useLiquiditySettings";
import { postOrder } from "@/services/api";
import { cn } from "@/lib/utils";

const YES_SUFFIX = "-yes";
const NO_SUFFIX = "-no";

export interface MarketControlPanelProps {
  /** Base market id (e.g. drake-album). YES/NO orders use {baseMarketId}-yes and {baseMarketId}-no. */
  baseMarketId?: string;
  /** Default operatorId for orders. */
  operatorId?: string;
  /** Default userId for orders. */
  userId?: string;
  className?: string;
  /** Rendered between Quotes and Quick Order (e.g. Net Positions). */
  children?: ReactNode;
  /** When set (e.g. from Order Book price click), syncs to Quick Order price field. */
  priceFromOrderBook?: number | null;
  /** Suggested side from Order Book: Ask click → Buy, Bid click → Sell. */
  suggestedSideFromOrderBook?: "Buy" | "Sell" | null;
}

export function MarketControlPanel({
  baseMarketId = "drake-album",
  operatorId = "operator-1",
  userId = "user-dashboard",
  className,
  children,
  priceFromOrderBook,
  suggestedSideFromOrderBook,
}: MarketControlPanelProps) {
  const [price, setPrice] = useState(0.85);
  const [quantity, setQuantity] = useState(10);

  useEffect(() => {
    if (priceFromOrderBook != null && Number.isFinite(priceFromOrderBook)) {
      setPrice(priceFromOrderBook);
    }
  }, [priceFromOrderBook]);
  const [orderError, setOrderError] = useState<string | null>(null);
  const [orderSuccess, setOrderSuccess] = useState<string | null>(null);

  const quotesMarketId = baseMarketId;
  const { data: quotesData, error: quotesError, isLoading: quotesLoading, refetch: refetchQuotes } = useLiquidityQuotes(quotesMarketId);
  const { data: settingsData, error: settingsError, isLoading: settingsLoading, toggleProvider } = useLiquiditySettings();

  const outcomeIdYes = baseMarketId.endsWith(YES_SUFFIX) ? baseMarketId : `${baseMarketId}${YES_SUFFIX}`;
  const outcomeIdNo = baseMarketId.endsWith(NO_SUFFIX) ? baseMarketId : `${baseMarketId}${NO_SUFFIX}`;

  const placeOrder = async (
    outcomeId: string,
    side: "Buy" | "Sell"
  ) => {
    setOrderError(null);
    setOrderSuccess(null);
    try {
      await postOrder({
        marketId: outcomeId,
        price,
        quantity,
        side,
        operatorId,
        userId,
      });
      setOrderSuccess(`${side} ${outcomeId}`);
      refetchQuotes();
    } catch (e) {
      setOrderError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <div className={cn("space-y-4", className)}>
      {/* Quotes from providers */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base font-semibold">Quotes by provider</CardTitle>
          <p className="text-muted-foreground text-xs">Market: {quotesMarketId}</p>
        </CardHeader>
        <CardContent className="pt-0">
          {quotesLoading && (
            <div className="py-4 text-center text-muted-foreground text-sm">
              Loading…
            </div>
          )}
          {quotesError && (
            <p className="text-amber-600 dark:text-amber-400 text-sm">{quotesError.message}</p>
          )}
          {quotesData?.providerQuotes?.length ? (
            <Table>
              <TableHeader>
                <TableRow className="border-b bg-muted/50">
                  <TableHead className="text-xs">Provider</TableHead>
                  <TableHead className="text-right text-xs">Best bid</TableHead>
                  <TableHead className="text-right text-xs">Best ask</TableHead>
                  <TableHead className="text-right text-xs">Spread</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {quotesData.providerQuotes.map((q) => (
                  <TableRow key={q.providerId}>
                    <TableCell className="font-medium">{q.providerId}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {q.bestBid != null ? q.bestBid.toFixed(2) : "—"}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {q.bestAsk != null ? q.bestAsk.toFixed(2) : "—"}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {q.spread != null ? q.spread.toFixed(2) : "—"}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : !quotesLoading && !quotesError && (
            <p className="text-muted-foreground text-sm">No quotes</p>
          )}
        </CardContent>
      </Card>

      {children}

      {/* Order: price, quantity, YES/NO buttons */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base font-semibold">Quick order (ContractType YES/NO)</CardTitle>
          <p className="text-muted-foreground text-xs">
            Price 0.00–1.00 · Same price/qty for all
            {suggestedSideFromOrderBook && (
              <span className="ml-1 rounded bg-zinc-200 px-1.5 py-0.5 text-xs font-medium dark:bg-zinc-700">
                Suggested: {suggestedSideFromOrderBook}
              </span>
            )}
          </p>
        </CardHeader>
        <CardContent className="pt-0 space-y-3">
          <div className="flex flex-wrap items-center gap-3">
            <label className="flex items-center gap-2 text-sm">
              Price
              <input
                type="number"
                min={0}
                max={1}
                step={0.01}
                value={price}
                onChange={(e) => setPrice(Number(e.target.value))}
                className="w-20 rounded border bg-background px-2 py-1 text-sm tabular-nums"
              />
            </label>
            <label className="flex items-center gap-2 text-sm">
              Qty
              <input
                type="number"
                min={1}
                value={quantity}
                onChange={(e) => setQuantity(Number(e.target.value))}
                className="w-20 rounded border bg-background px-2 py-1 text-sm tabular-nums"
              />
            </label>
          </div>
          {orderError && (
            <p className="text-amber-600 dark:text-amber-400 text-sm">{orderError}</p>
          )}
          {orderSuccess && (
            <p className="text-emerald-600 dark:text-emerald-400 text-sm">{orderSuccess}</p>
          )}
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Button
              size="sm"
              variant="default"
              className="bg-emerald-600 hover:bg-emerald-700"
              onClick={() => placeOrder(outcomeIdYes, "Buy")}
            >
              Buy YES
            </Button>
            <Button
              size="sm"
              variant="destructive"
              onClick={() => placeOrder(outcomeIdYes, "Sell")}
            >
              Sell YES
            </Button>
            <Button
              size="sm"
              variant="default"
              className="bg-emerald-600 hover:bg-emerald-700"
              onClick={() => placeOrder(outcomeIdNo, "Buy")}
            >
              Buy NO
            </Button>
            <Button
              size="sm"
              variant="destructive"
              onClick={() => placeOrder(outcomeIdNo, "Sell")}
            >
              Sell NO
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Provider toggles */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base font-semibold">Liquidity providers (toggle)</CardTitle>
          <p className="text-muted-foreground text-xs">Enable/disable providers at runtime</p>
        </CardHeader>
        <CardContent className="pt-0">
          {settingsLoading && (
            <div className="py-2 text-muted-foreground text-sm">Loading…</div>
          )}
          {settingsError && (
            <p className="text-amber-600 dark:text-amber-400 text-sm">{settingsError.message}</p>
          )}
          {settingsData?.effectiveEnabled != null && (
            <div className="flex flex-wrap gap-2">
              {["Internal", "Partner_A", "Partner_B"].map((id) => {
                const enabled = settingsData.effectiveEnabled.some(
                  (p) => p.toLowerCase() === id.toLowerCase()
                );
                return (
                  <label
                    key={id}
                    className={cn(
                      "inline-flex cursor-pointer items-center gap-2 rounded-md border px-3 py-2 text-sm transition-colors",
                      enabled
                        ? "border-emerald-500/50 bg-emerald-500/10 dark:bg-emerald-500/20"
                        : "border-zinc-300 dark:border-zinc-600"
                    )}
                  >
                    <input
                      type="checkbox"
                      checked={enabled}
                      onChange={() => toggleProvider(id)}
                      className="rounded"
                    />
                    <span>{id}</span>
                  </label>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
