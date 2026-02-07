"use client";

import { useEffect } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useExchangeHub } from "@/hooks/useExchangeHub";
import { usePosition } from "@/hooks/usePosition";
import { cn } from "@/lib/utils";

export interface PortfolioViewProps {
  userId: string;
  /** Optional market filter (outcomeId). */
  marketId?: string;
  className?: string;
  /** When provided, refetches positions on SignalR TradeMatched for real-time updates. */
  exchangeHub?: ReturnType<typeof useExchangeHub>;
}

export function PortfolioView({ userId, marketId, className, exchangeHub }: PortfolioViewProps) {
  const { data, error, isLoading, refetch } = usePosition(userId, marketId);

  useEffect(() => {
    if (!exchangeHub || !userId.trim()) return;
    const watchMarket = marketId?.trim() || "drake-album";
    return exchangeHub.subscribeToOrderBookInvalidate(watchMarket, refetch);
  }, [exchangeHub, userId, marketId, refetch]);

  return (
    <Card className={cn("overflow-hidden", className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">Net position</CardTitle>
        <p className="text-muted-foreground text-xs">
          User: {userId}
          {marketId ? ` · Market: ${marketId}` : " · All markets"}
        </p>
      </CardHeader>
      <CardContent className="pt-0">
        {error && (
          <p className="mb-2 text-xs text-amber-600 dark:text-amber-400" role="status">
            {error.message}
          </p>
        )}
        {isLoading ? (
          <div className="flex h-32 items-center justify-center text-muted-foreground text-sm">
            Loading…
          </div>
        ) : !data?.positions?.length ? (
          <div className="flex h-32 items-center justify-center text-muted-foreground text-sm">
            No positions
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow className="border-b bg-muted/50 hover:bg-muted/50">
                <TableHead className="text-muted-foreground text-xs font-medium">Outcome</TableHead>
                <TableHead className="text-right text-muted-foreground text-xs font-medium">
                  Net
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.positions.map((p) => (
                <TableRow key={p.outcomeId}>
                  <TableCell className="font-mono text-sm">{p.outcomeId}</TableCell>
                  <TableCell
                    className={cn(
                      "text-right tabular-nums font-medium",
                      p.netQuantity > 0
                        ? "text-emerald-600 dark:text-emerald-400"
                        : p.netQuantity < 0
                          ? "text-red-600 dark:text-red-400"
                          : "text-muted-foreground"
                    )}
                  >
                    {p.netQuantity > 0 ? "+" : ""}
                    {p.netQuantity.toLocaleString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
