"use client";

import { useCallback, useEffect, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { API_BASE } from "@/services/api";

/** Server method name for trade matches (must match backend ExchangeHub.TradeMatchedMethod). */
export const TRADE_MATCHED = "TradeMatched";

export type HubConnectionState = "Connecting" | "Connected" | "Disconnected";

export interface TradeMatchedPayload {
  marketId: string;
  price: number;
  quantity: number;
  side: string;
}

/** Shape suitable for appending to TradeHistory (id and time generated client-side). */
export interface LiveTrade {
  id: string;
  time: string;
  price: number;
  size: number;
  side: "Buy" | "Sell";
}

const HUB_URL = `${API_BASE}/hubs/exchange`;

/**
 * Connects to http://localhost:5051/hubs/exchange and exposes connection state
 * plus subscription to TradeMatched for a given marketId.
 */
export function useExchangeHub() {
  const [connectionState, setConnectionState] = useState<HubConnectionState>("Disconnected");
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    conn.onreconnecting(() => setConnectionState("Connecting"));
    conn.onreconnected(() => setConnectionState("Connected"));
    conn.onclose(() => setConnectionState("Disconnected"));

    console.log("[SignalR] Connecting to", HUB_URL);
    conn
      .start()
      .then(() => {
        setConnectionState("Connected");
        setConnection(conn);
        console.log("[SignalR] Exchange hub connected:", HUB_URL);
      })
      .catch((err) => {
        console.warn("[SignalR] Connection failed:", err);
        setConnectionState("Disconnected");
      });

    return () => {
      conn.stop().catch(() => {});
      setConnection(null);
      setConnectionState("Disconnected");
    };
  }, []);

  const subscribeToTrade = useCallback(
    (marketId: string, onTrade: (trade: LiveTrade) => void) => {
      if (!connection) return () => {};

      const handler = (payload: TradeMatchedPayload) => {
        if (String(payload?.marketId).trim().toLowerCase() !== marketId.trim().toLowerCase())
          return;
        const trade: LiveTrade = {
          id: `live-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`,
          time: new Date().toISOString(),
          price: Number(payload.price),
          size: Number(payload.quantity),
          side: payload.side === "Sell" ? "Sell" : "Buy",
        };
        onTrade(trade);
      };

      connection.on(TRADE_MATCHED, handler);
      return () => {
        connection.off(TRADE_MATCHED, handler);
      };
    },
    [connection]
  );

  const subscribeToOrderBookInvalidate = useCallback(
    (marketId: string, onInvalidate: () => void) => {
      if (!connection) return () => {};
      const handler = (payload: TradeMatchedPayload) => {
        if (String(payload?.marketId).trim().toLowerCase() === marketId.trim().toLowerCase())
          onInvalidate();
      };
      connection.on(TRADE_MATCHED, handler);
      return () => connection.off(TRADE_MATCHED, handler);
    },
    [connection]
  );

  return {
    connectionState,
    subscribeToTrade,
    subscribeToOrderBookInvalidate,
  };
}
