"use client";

import { useEffect } from "react";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useExchangeHub } from "@/hooks/useExchangeHub";
import { useOrderBook, type OrderBookLevel } from "@/hooks/useOrderBook";
import { cn } from "@/lib/utils";

const DEPTH_ASK = "bg-red-500/25";
const DEPTH_BID = "bg-emerald-500/25";
const TEXT_ASK = "text-red-700 dark:text-red-400";
const TEXT_BID = "text-emerald-700 dark:text-emerald-400";

/** One price level after grouping (price + total size). */
type PriceLevel = { price: number; quantity: number };

/** Group orders by price and sum quantity so each price appears once with total size. */
function groupByPrice(levels: OrderBookLevel[]): PriceLevel[] {
  const byPrice = new Map<number, number>();
  for (const l of levels) {
    const p = Number(l.price);
    byPrice.set(p, (byPrice.get(p) ?? 0) + Number(l.quantity));
  }
  return Array.from(byPrice.entries(), ([price, quantity]) => ({ price, quantity }));
}

function DepthRow({
  level,
  maxQty,
  isAsk,
  onPriceClick,
}: {
  level: PriceLevel;
  maxQty: number;
  isAsk: boolean;
  onPriceClick?: (price: number, side: "Buy" | "Sell") => void;
}) {
  const depthPercent = maxQty > 0 ? Math.min(100, (level.quantity / maxQty) * 100) : 0;
  const side: "Buy" | "Sell" = isAsk ? "Buy" : "Sell";

  return (
    <TableRow
      className={cn(
        "relative cursor-pointer border-0 transition-colors",
        onPriceClick && "hover:bg-zinc-100 dark:hover:bg-zinc-800/80"
      )}
      onClick={() => onPriceClick?.(level.price, side)}
      role={onPriceClick ? "button" : undefined}
      tabIndex={onPriceClick ? 0 : undefined}
      onKeyDown={
        onPriceClick
          ? (e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onPriceClick(level.price, side);
              }
            }
          : undefined
      }
    >
      <TableCell
        colSpan={2}
        className="relative h-8 p-0 align-middle"
      >
        <div className="relative flex h-8 w-full items-center">
          <div
            className={cn(
              "absolute inset-y-0 left-0 rounded-sm transition-[width]",
              isAsk ? DEPTH_ASK : DEPTH_BID
            )}
            style={{ width: `${depthPercent}%` }}
            aria-hidden
          />
          <div className="relative z-10 flex w-full flex-1 justify-between px-3 text-sm tabular-nums">
            <span className={cn("font-medium", isAsk ? TEXT_ASK : TEXT_BID)}>
              {level.price.toFixed(2)}
            </span>
            <span className="text-muted-foreground">
              {level.quantity.toLocaleString()}
            </span>
          </div>
        </div>
      </TableCell>
    </TableRow>
  );
}

export interface OrderBookProps {
  outcomeId?: string;
  className?: string;
  /** When provided, order book refetches on SignalR TradeMatched for real-time updates. */
  exchangeHub?: ReturnType<typeof useExchangeHub>;
  /** When provided, price rows are clickable; called with (price, side). Ask → Buy, Bid → Sell. */
  onPriceClick?: (price: number, side: "Buy" | "Sell") => void;
}

export function OrderBook({ outcomeId = "drake-album", className, exchangeHub, onPriceClick }: OrderBookProps) {
  const { data, error, isLoading, isMock, refetch } = useOrderBook(outcomeId);

  useEffect(() => {
    if (!exchangeHub) return;
    return exchangeHub.subscribeToOrderBookInvalidate(outcomeId, refetch);
  }, [exchangeHub, outcomeId, refetch]);

  const groupedAsks = data?.asks?.length ? groupByPrice(data.asks).sort((a, b) => a.price - b.price) : [];
  const groupedBids = data?.bids?.length ? groupByPrice(data.bids).sort((a, b) => b.price - a.price) : [];
  const maxAskQty = groupedAsks.length ? Math.max(...groupedAsks.map((a) => a.quantity), 1) : 1;
  const maxBidQty = groupedBids.length ? Math.max(...groupedBids.map((b) => b.quantity), 1) : 1;

  return (
    <Card className={cn("overflow-hidden", className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">Order Book</CardTitle>
        <p className="text-muted-foreground text-xs">
          {isMock && "Using mock data · "}
          Outcome: {outcomeId}
          {onPriceClick && " · Click a price to use in Quick Order"}
        </p>
      </CardHeader>
      <CardContent className="pt-0">
        {error && isMock && (
          <p className="mb-2 text-xs text-amber-600 dark:text-amber-400" role="status">
            API unavailable: {error.message}
          </p>
        )}
        {isLoading ? (
          <div className="flex h-48 items-center justify-center text-muted-foreground text-sm">
            Loading order book…
          </div>
        ) : !data ? (
          <div className="flex h-48 items-center justify-center text-muted-foreground text-sm">
            No data
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow className="border-b bg-muted/50 hover:bg-muted/50">
                <TableHead className="text-muted-foreground text-xs font-medium">
                  Price
                </TableHead>
                <TableHead className="text-right text-muted-foreground text-xs font-medium">
                  Size
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {/* Asks (Sell) — red, top; grouped by price */}
              {groupedAsks.length > 0 && (
                <>
                  <TableRow className="border-0 bg-transparent">
                    <TableCell
                      colSpan={2}
                      className="py-1 text-left text-xs font-medium text-red-600 dark:text-red-400"
                    >
                      Asks
                    </TableCell>
                  </TableRow>
                  {groupedAsks.map((level) => (
                    <DepthRow
                      key={`ask-${level.price}`}
                      level={level}
                      maxQty={maxAskQty}
                      isAsk
                      onPriceClick={onPriceClick}
                    />
                  ))}
                </>
              )}
              {/* Bids (Buy) — green, bottom; grouped by price */}
              {groupedBids.length > 0 && (
                <>
                  <TableRow className="border-0 bg-transparent">
                    <TableCell
                      colSpan={2}
                      className="py-1 text-left text-xs font-medium text-emerald-600 dark:text-emerald-400"
                    >
                      Bids
                    </TableCell>
                  </TableRow>
                  {groupedBids.map((level) => (
                    <DepthRow
                      key={`bid-${level.price}`}
                      level={level}
                      maxQty={maxBidQty}
                      isAsk={false}
                      onPriceClick={onPriceClick}
                    />
                  ))}
                </>
              )}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
