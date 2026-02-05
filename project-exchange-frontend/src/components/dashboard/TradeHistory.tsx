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
import { useTradeHistory, type TradeRecord } from "@/hooks/useTradeHistory";
import { cn } from "@/lib/utils";

function formatTime(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleTimeString(undefined, {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false,
    });
  } catch {
    return iso;
  }
}

export interface TradeHistoryProps {
  outcomeId?: string;
  className?: string;
  /** When provided, live trades from SignalR are appended to the list. */
  exchangeHub?: ReturnType<typeof useExchangeHub>;
}

export function TradeHistory({
  outcomeId = "drake-album",
  className,
  exchangeHub,
}: TradeHistoryProps) {
  const { data, error, isLoading, appendTrade } = useTradeHistory(outcomeId);

  useEffect(() => {
    if (!exchangeHub) return;
    return exchangeHub.subscribeToTrade(outcomeId, (liveTrade) => {
      appendTrade({
        id: liveTrade.id,
        time: liveTrade.time,
        price: liveTrade.price,
        size: liveTrade.size,
        side: liveTrade.side,
      });
    });
  }, [exchangeHub, outcomeId, appendTrade]);

  return (
    <Card className={cn("overflow-hidden", className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">Trade History</CardTitle>
        <p className="text-muted-foreground text-xs">Outcome: {outcomeId}</p>
      </CardHeader>
      <CardContent className="pt-0">
        {error && (
          <p
            className="mb-2 text-xs text-amber-600 dark:text-amber-400"
            role="status"
          >
            API: {error.message}
          </p>
        )}
        {isLoading ? (
          <div className="flex h-48 items-center justify-center text-muted-foreground text-sm">
            Loading trades…
          </div>
        ) : !data ? (
          <div className="flex h-48 items-center justify-center text-muted-foreground text-sm">
            No data
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/50 hover:bg-muted/50">
                <TableHead className="text-muted-foreground text-xs font-medium">
                  Time
                </TableHead>
                <TableHead className="text-muted-foreground text-xs font-medium">
                  Price
                </TableHead>
                <TableHead className="text-muted-foreground text-xs font-medium">
                  Size
                </TableHead>
                <TableHead className="text-right text-muted-foreground text-xs font-medium">
                  Side
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.trades.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={4}
                    className="h-24 text-center text-muted-foreground text-sm"
                  >
                    No trades yet
                  </TableCell>
                </TableRow>
              ) : (
                data.trades.map((trade) => (
                  <TradeRow key={trade.id} trade={trade} />
                ))
              )}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

function TradeRow({ trade }: { trade: TradeRecord }) {
  const isBuy = trade.side === "Buy";
  const sideClass = isBuy
    ? "text-emerald-700 dark:text-emerald-400 font-medium"
    : "text-red-700 dark:text-red-400 font-medium";

  return (
    <TableRow className="border-b transition-colors hover:bg-muted/30">
      <TableCell className="text-muted-foreground text-xs tabular-nums">
        {formatTime(trade.time)}
      </TableCell>
      <TableCell className="tabular-nums">{trade.price.toFixed(2)}</TableCell>
      <TableCell className="tabular-nums">{trade.size.toLocaleString()}</TableCell>
      <TableCell className={cn("text-right", sideClass)}>{trade.side}</TableCell>
    </TableRow>
  );
}
