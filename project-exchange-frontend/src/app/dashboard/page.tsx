"use client";

import { OrderBook } from "@/components/dashboard/OrderBook";
import { SignalRStatus } from "@/components/dashboard/SignalRStatus";
import { TradeHistory } from "@/components/dashboard/TradeHistory";
import { useExchangeHub } from "@/hooks/useExchangeHub";

export default function DashboardPage() {
  const hub = useExchangeHub();

  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b border-zinc-200 dark:border-zinc-800 px-6 py-4 flex items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">Project Exchange</h1>
          <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">
            Clearing &amp; settlement · Drake module
          </p>
        </div>
        <SignalRStatus connectionState={hub.connectionState} />
      </header>

      <main className="p-6 max-w-7xl mx-auto">
        <div className="grid gap-6 lg:grid-cols-2">
          <section aria-label="Order book">
            <OrderBook outcomeId="drake-album" exchangeHub={hub} />
          </section>

          <section aria-label="Trade history">
            <TradeHistory outcomeId="drake-album" exchangeHub={hub} />
          </section>
        </div>
      </main>
    </div>
  );
}
