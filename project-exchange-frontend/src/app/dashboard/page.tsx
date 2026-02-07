"use client";

import Link from "next/link";
import { useState } from "react";
import {
  OrderBook,
  MarketControlPanel,
  PortfolioView,
  SignalRStatus,
  TradeHistory,
} from "@/components/dashboard";
import { useExchangeHub } from "@/hooks/useExchangeHub";

export default function DashboardPage() {
  const hub = useExchangeHub();
  const [priceFromOrderBook, setPriceFromOrderBook] = useState<number | null>(null);
  const [suggestedSideFromOrderBook, setSuggestedSideFromOrderBook] = useState<"Buy" | "Sell" | null>(null);

  const handleOrderBookPriceClick = (price: number, side: "Buy" | "Sell") => {
    setPriceFromOrderBook(price);
    setSuggestedSideFromOrderBook(side);
  };

  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b border-zinc-200 dark:border-zinc-800 px-6 py-4 flex items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <div>
            <h1 className="text-xl font-semibold">Project Exchange</h1>
            <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">
              Clearing &amp; settlement · Drake module · Kalshi-dödare
            </p>
          </div>
          <Link
            href="/portfolio"
            className="text-sm text-muted-foreground hover:text-foreground"
          >
            Portfolio →
          </Link>
        </div>
        <SignalRStatus connectionState={hub.connectionState} />
      </header>

      <main className="p-4 md:p-6 max-w-7xl mx-auto">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-12 lg:gap-6">
          {/* Left column (60–70%): Order Book + Trade History */}
          <div className="flex flex-col gap-4 lg:col-span-8">
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <section aria-label="Order book" className="min-w-0">
                <OrderBook
                  outcomeId="drake-album"
                  exchangeHub={hub}
                  onPriceClick={handleOrderBookPriceClick}
                />
              </section>
              <section aria-label="Trade history" className="min-w-0">
                <TradeHistory outcomeId="drake-album" exchangeHub={hub} />
              </section>
            </div>
          </div>

          {/* Right column (30–40%): Quotes, Net Positions, Quick Order, Liquidity Providers */}
          <aside className="flex flex-col gap-4 lg:col-span-4">
            <MarketControlPanel
              baseMarketId="drake-album"
              priceFromOrderBook={priceFromOrderBook}
              suggestedSideFromOrderBook={suggestedSideFromOrderBook}
            >
              <PortfolioView userId="user-dashboard" exchangeHub={hub} />
            </MarketControlPanel>
          </aside>
        </div>
      </main>
    </div>
  );
}
