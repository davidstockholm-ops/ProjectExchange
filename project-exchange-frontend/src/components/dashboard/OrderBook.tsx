"use client";

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
import { useOrderBook, type OrderBookLevel } from "@/hooks/useOrderBook";
import { cn } from "@/lib/utils";

const DEPTH_ASK = "bg-red-500/25";
const DEPTH_BID = "bg-emerald-500/25";
const TEXT_ASK = "text-red-700 dark:text-red-400";
const TEXT_BID = "text-emerald-700 dark:text-emerald-400";

function DepthRow({
  level,
  maxQty,
  isAsk,
}: {
  level: OrderBookLevel;
  maxQty: number;
  isAsk: boolean;
}) {
  const depthPercent = maxQty > 0 ? Math.min(100, (level.quantity / maxQty) * 100) : 0;

  return (
    <TableRow className="relative border-0">
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
}

export function OrderBook({ outcomeId = "outcome-demo", className }: OrderBookProps) {
  const { data, error, isLoading, isMock } = useOrderBook(outcomeId);

  const maxAskQty =
    data?.asks?.length ?
      Math.max(...data.asks.map((a) => a.quantity), 1)
    : 1;
  const maxBidQty =
    data?.bids?.length ?
      Math.max(...data.bids.map((b) => b.quantity), 1)
    : 1;

  return (
    <Card className={cn("overflow-hidden", className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">Order Book</CardTitle>
        <p className="text-muted-foreground text-xs">
          {isMock && "Using mock data · "}
          Outcome: {outcomeId}
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
              {/* Asks (Sell) — red, top */}
              {data.asks.length > 0 && (
                <>
                  <TableRow className="border-0 bg-transparent">
                    <TableCell
                      colSpan={2}
                      className="py-1 text-left text-xs font-medium text-red-600 dark:text-red-400"
                    >
                      Asks
                    </TableCell>
                  </TableRow>
                  {data.asks.map((level) => (
                    <DepthRow
                      key={level.orderId}
                      level={level}
                      maxQty={maxAskQty}
                      isAsk
                    />
                  ))}
                </>
              )}
              {/* Bids (Buy) — green, bottom */}
              {data.bids.length > 0 && (
                <>
                  <TableRow className="border-0 bg-transparent">
                    <TableCell
                      colSpan={2}
                      className="py-1 text-left text-xs font-medium text-emerald-600 dark:text-emerald-400"
                    >
                      Bids
                    </TableCell>
                  </TableRow>
                  {data.bids.map((level) => (
                    <DepthRow
                      key={level.orderId}
                      level={level}
                      maxQty={maxBidQty}
                      isAsk={false}
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
