import type { Metadata } from "next";
import { OrderBook } from "@/components/dashboard/OrderBook";
import { TradeHistory } from "@/components/dashboard/TradeHistory";

export const metadata: Metadata = {
  title: "Dashboard | Project Exchange",
  description: "Order book and trade history for Project Exchange",
};

export default function DashboardPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b border-zinc-200 dark:border-zinc-800 px-6 py-4">
        <h1 className="text-xl font-semibold">Project Exchange</h1>
        <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">
          Clearing &amp; settlement · Drake module
        </p>
      </header>

      <main className="p-6 max-w-7xl mx-auto">
        <div className="grid gap-6 lg:grid-cols-2">
          <section aria-label="Order book">
            <OrderBook outcomeId="outcome-demo" />
          </section>

          <section aria-label="Trade history">
            <TradeHistory outcomeId="outcome-demo" />
          </section>
        </div>
      </main>
    </div>
  );
}
