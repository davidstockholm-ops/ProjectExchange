"use client";

import Link from "next/link";
import { PortfolioView, SignalRStatus } from "@/components/dashboard";
import { useExchangeHub } from "@/hooks/useExchangeHub";
import { useState } from "react";

const DEFAULT_USER = "user-dashboard";

export default function PortfolioPage() {
  const hub = useExchangeHub();
  const [userId, setUserId] = useState(DEFAULT_USER);

  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b border-zinc-200 dark:border-zinc-800 px-6 py-4 flex items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <Link
            href="/dashboard"
            className="text-sm text-muted-foreground hover:text-foreground"
          >
            ← Dashboard
          </Link>
          <h1 className="text-xl font-semibold">Portfolio (positions)</h1>
        </div>
        <SignalRStatus connectionState={hub.connectionState} />
      </header>

      <main className="p-6 max-w-4xl mx-auto">
        <div className="mb-4 flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-2 text-sm">
            User ID
            <input
              type="text"
              value={userId}
              onChange={(e) => setUserId(e.target.value.trim() || DEFAULT_USER)}
              placeholder={DEFAULT_USER}
              className="rounded border bg-background px-3 py-2 text-sm w-48"
            />
          </label>
        </div>
        <PortfolioView userId={userId} exchangeHub={hub} />
        <p className="mt-4 text-muted-foreground text-xs">
          Positions are updated in real time when trades occur (SignalR). Net position is
          computed from TradeMatched events (buyer +qty, seller −qty).
        </p>
      </main>
    </div>
  );
}
