"use client";

import { useState, useCallback, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { followLeader, getFollowing, type FollowingResponse } from "@/services/api";
import { cn } from "@/lib/utils";

/** Mock known leader profiles (ROI display). Backend stores follow relation by leaderId. */
const MOCK_LEADERS = [
  { id: "DrakeOfficial", displayName: "DrakeOfficial", roi: 145 },
  { id: "OVO-Insider", displayName: "OVO-Insider", roi: 82 },
  { id: "Whale-Alpha", displayName: "Whale-Alpha", roi: 12 },
] as const;

export interface CopyTradingPanelProps {
  /** Current user (follower) ID; used when clicking Copy. */
  userId?: string;
  className?: string;
}

export function CopyTradingPanel({
  userId = "user-dashboard",
  className,
}: CopyTradingPanelProps) {
  const [following, setFollowing] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const loadFollowing = useCallback(async () => {
    try {
      const data: FollowingResponse = await getFollowing(userId);
      setFollowing(new Set(data.leaderIds));
    } catch {
      setFollowing(new Set());
    }
  }, [userId]);

  useEffect(() => {
    loadFollowing();
  }, [loadFollowing]);

  const handleCopy = async (leaderId: string) => {
    setError(null);
    setSuccess(null);
    setLoading(leaderId);
    try {
      await followLeader(userId, leaderId);
      setFollowing((prev) => new Set(prev).add(leaderId));
      setSuccess(`Now following ${leaderId}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(null);
    }
  };

  return (
    <Card className={cn(className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">Copy trading</CardTitle>
        <p className="text-muted-foreground text-xs">
          Follow a leader — their trades are mirrored to you
        </p>
      </CardHeader>
      <CardContent className="pt-0">
        {error && (
          <p className="mb-2 text-sm text-amber-600 dark:text-amber-400">{error}</p>
        )}
        {success && (
          <p className="mb-2 text-sm text-emerald-600 dark:text-emerald-400">{success}</p>
        )}
        <ul className="space-y-3">
          {MOCK_LEADERS.map((leader) => {
            const isFollowing = following.has(leader.id);
            const busy = loading === leader.id;
            return (
              <li
                key={leader.id}
                className="flex items-center justify-between gap-2 rounded-md border border-zinc-200 px-3 py-2 dark:border-zinc-700"
              >
                <div>
                  <span className="font-medium">{leader.displayName}</span>
                  <span className="ml-2 text-xs text-muted-foreground">
                    ROI {leader.roi}%
                  </span>
                </div>
                <Button
                  size="sm"
                  variant={isFollowing ? "secondary" : "default"}
                  disabled={busy || isFollowing}
                  onClick={() => handleCopy(leader.id)}
                >
                  {busy ? "…" : isFollowing ? "Following" : "Copy"}
                </Button>
              </li>
            );
          })}
        </ul>
      </CardContent>
    </Card>
  );
}
