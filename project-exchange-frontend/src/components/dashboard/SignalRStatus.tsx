"use client";

import type { HubConnectionState } from "@/hooks/useExchangeHub";
import { cn } from "@/lib/utils";

export function SignalRStatus({ connectionState }: { connectionState: HubConnectionState }) {
  const isConnected = connectionState === "Connected";
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium",
        isConnected
          ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300"
          : "bg-zinc-200 text-zinc-600 dark:bg-zinc-700 dark:text-zinc-300"
      )}
      role="status"
      aria-label={`SignalR: ${connectionState}`}
    >
      <span
        className={cn(
          "h-1.5 w-1.5 rounded-full",
          isConnected ? "bg-emerald-500" : "bg-zinc-500"
        )}
      />
      {connectionState}
    </span>
  );
}
