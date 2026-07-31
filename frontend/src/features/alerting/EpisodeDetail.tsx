import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api, type Episode } from "@/api/client";
import { useCurrentUser } from "@/api/queries";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { describeMillis } from "@/lib/duration";
import { formatTime } from "@/lib/format";

import { StateBadge } from "./StateBadge";

/**
 * The expanded row: the Episode's human marks with the two hands that set them, and the history
 * of its kind of trouble — every Episode of this (Service, Fingerprint) pair, so a recurrence is
 * read next to who solved it last time.
 */
export function EpisodeDetail({ episode }: { episode: Episode }) {
  const queryClient = useQueryClient();
  const currentUser = useCurrentUser();
  const [solveOpen, setSolveOpen] = useState(false);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["alerts", "episodes"] });

  const acknowledge = useMutation({
    mutationFn: async () => {
      const { response } = await api.POST("/api/alerting/episodes/{id}/acknowledge", {
        params: { path: { id: episode.id } },
      });
      if (response.status === 409) throw new Error("Already solved — a Solved Episode is never acknowledged.");
      if (!response.ok) throw new Error("The acknowledgement was not saved.");
    },
    onSettled: refresh,
  });

  const withdraw = useMutation({
    mutationFn: async () => {
      const { response } = await api.DELETE("/api/alerting/episodes/{id}/acknowledgement", {
        params: { path: { id: episode.id } },
      });
      if (!response.ok) throw new Error("The acknowledgement was not withdrawn.");
    },
    onSettled: refresh,
  });

  const solve = useMutation({
    mutationFn: async () => {
      const { response } = await api.POST("/api/alerting/episodes/{id}/solve", {
        params: { path: { id: episode.id } },
      });
      if (response.status === 409) throw new Error("Already solved by someone else.");
      if (!response.ok) throw new Error("The verdict was not saved.");
    },
    onSuccess: () => setSolveOpen(false),
    onSettled: refresh,
  });

  const history = useQuery({
    queryKey: ["alerts", "episode-history", episode.serviceId, episode.fingerprint],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: { serviceId: episode.serviceId, fingerprint: episode.fingerprint, limit: 50 },
        },
      });
      if (error !== undefined) throw new Error("Failed to load the history");
      return data;
    },
  });

  const myName = currentUser.data?.displayName ?? currentUser.data?.email;
  const heldByMe = episode.acknowledgedBy !== null && episode.acknowledgedBy === myName;
  const lastMatchAgo = describeMillis(Date.now() - Date.parse(episode.lastMatchAt));
  const failure = acknowledge.error ?? withdraw.error ?? solve.error;

  return (
    <div className="border-b border-[#101F31] bg-[#0B1826] px-5 py-4">
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
        <span className="font-mono text-[11.5px] text-[#7D93AA]">
          {episode.acknowledgedBy !== null
            ? `Acknowledged by ${episode.acknowledgedBy} at ${formatTime(episode.acknowledgedAt!)}`
            : episode.state === "Solved"
              ? `Solved by ${episode.solvedBy ?? "a user no longer here"} at ${formatTime(episode.solvedAt!)}`
              : "Nobody is on this yet."}
        </span>

        {episode.state !== "Solved" && (
          <span className="flex items-center gap-2">
            {!heldByMe && (
              <Button
                size="sm"
                variant="outline"
                disabled={acknowledge.isPending}
                onClick={() => acknowledge.mutate()}
              >
                {episode.acknowledgedBy !== null ? "Take over" : "Acknowledge"}
              </Button>
            )}
            {episode.acknowledgedBy !== null && (
              <Button
                size="sm"
                variant="ghost"
                disabled={withdraw.isPending}
                onClick={() => withdraw.mutate()}
              >
                Withdraw
              </Button>
            )}
            <Button size="sm" onClick={() => setSolveOpen(true)}>
              Solve
            </Button>
          </span>
        )}

        {failure !== null && failure !== undefined && (
          <span className="text-[11.5px] text-destructive">{failure.message}</span>
        )}
      </div>

      <div className="mt-4">
        <p className="font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">
          THIS KIND OF TROUBLE ({(history.data?.items.length ?? 1) - 1} earlier)
        </p>
        <div className="mt-1.5">
          {(history.data?.items ?? []).map(item => (
            <div
              key={item.id}
              className="grid h-[29px] grid-cols-[150px_96px_110px_150px_1fr] items-center gap-3.5 border-t border-[#101F31] font-mono text-[11.5px] text-[#7D93AA]"
            >
              <span className="whitespace-nowrap">{formatTime(item.openedAt)}</span>
              <StateBadge state={item.state} />
              <span className="whitespace-nowrap">
                {describeMillis(
                  (item.closedAt != null ? Date.parse(item.closedAt) : Date.now())
                    - Date.parse(item.openedAt),
                )}
              </span>
              <span className="truncate whitespace-nowrap">
                <span className="text-severity-error">{item.errorCount} err</span>
                {Number(item.warnCount) > 0 && (
                  <span className="text-severity-warn"> · {item.warnCount} warn</span>
                )}
              </span>
              <span className="truncate">
                {item.id === episode.id
                  ? "← this one"
                  : item.solvedBy !== null
                    ? `solved by ${item.solvedBy}`
                    : item.acknowledgedBy !== null
                      ? `acknowledged by ${item.acknowledgedBy}`
                      : ""}
              </span>
            </div>
          ))}
          {history.isPending && (
            <p className="py-2 font-mono text-[11.5px] text-[#5F7590]">Loading…</p>
          )}
        </div>
      </div>

      <Dialog open={solveOpen} onOpenChange={setSolveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Solve this Episode?</DialogTitle>
            <DialogDescription>
              Solved is the verdict that the cause was fixed. It is final — there is no unsolve.
              {episode.state === "Open" && (
                <>
                  {" "}This Episode is still open (last match {lastMatchAgo} ago): solving closes
                  it on the spot, and a later matching log opens a new Episode with a new Alert.
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setSolveOpen(false)}>
              Cancel
            </Button>
            <Button disabled={solve.isPending} onClick={() => solve.mutate()}>
              Solve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
