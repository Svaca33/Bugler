import { useState } from "react";

import { useCurrentUser } from "@/api/queries";
import type { Episode } from "@/api/client";
import { Button } from "@/components/ui/button";
import { describeMillis } from "@/lib/duration";
import { serviceLabel } from "@/lib/serviceLabel";
import { severityRailClass } from "@/lib/severity";

import { describeLiveMillis } from "@/lib/duration";
import { LiveDuration, useNow } from "@/lib/LiveDuration";

import { clock, clockShort, ordinal } from "./format";
import { SolveDialog } from "./SolveDialog";
import type { KnownService } from "./serviceIndex";
import { StateBadge } from "./StateBadge";
import { useEpisodeActions } from "./useEpisodeActions";

/**
 * The triage surface: one card per Episode burning right now, with the live clock and the hands
 * that act on it. Fed by its own query so the rail's lifecycle boxes never hide what is on fire;
 * every other filter narrows it, keeping its number equal to the rail's Open count.
 */
export function OpenNowBand(props: {
  episodes: Episode[];
  services: Map<string, KnownService>;
  refreshedAt: number | undefined;
  onSelect: (id: string) => void;
}) {
  const now = useNow();
  const currentUser = useCurrentUser();

  if (props.episodes.length === 0) return null;

  const myName = currentUser.data?.displayName ?? currentUser.data?.email;
  const unheld = props.episodes.filter(e => e.acknowledgedBy === null).length;
  const oldest = Math.max(...props.episodes.map(e => now - Date.parse(e.openedAt)));

  return (
    <div className="flex flex-col gap-[9px] border-b border-[#17293D] bg-[#0C1A29] px-5 pt-3.5 pb-4">
      <div className="flex items-center gap-2.5">
        <span className="size-[7px] animate-[bpulse_1.6s_ease-in-out_infinite] rounded-full bg-severity-error-rail" />
        <h2 className="text-[13px] font-semibold tracking-[-0.1px]">Open now</h2>
        <span className="whitespace-nowrap font-mono text-[11px] text-[#7D93AA]">
          {props.episodes.length} episode{props.episodes.length === 1 ? "" : "s"} · {unheld} with
          nobody on it · oldest {describeLiveMillis(oldest)}
        </span>
        {props.refreshedAt !== undefined && (
          <span className="ml-auto flex items-center gap-1.5 whitespace-nowrap font-mono text-[10.5px] text-[#6E86A0]">
            <span className="size-1.5 animate-[bpulse_2.4s_ease-in-out_infinite] rounded-full bg-[#4E9E6A]" />
            live · refreshed {describeLiveMillis(now - props.refreshedAt)} ago
          </span>
        )}
      </div>

      {props.episodes.map(episode => (
        <OpenCard
          key={episode.id}
          episode={episode}
          known={props.services.get(episode.serviceId)}
          myName={myName}
          onSelect={props.onSelect}
        />
      ))}
    </div>
  );
}

function OpenCard(props: {
  episode: Episode;
  known: KnownService | undefined;
  myName: string | undefined;
  onSelect: (id: string) => void;
}) {
  const { episode } = props;
  const now = useNow();
  const actions = useEpisodeActions(episode.id);
  const [solveOpen, setSolveOpen] = useState(false);

  const severity = Number(episode.firstLogSeverity);
  const isError = severity >= 17;
  const heldByMe = episode.acknowledgedBy !== null && episode.acknowledgedBy === props.myName;
  const priorCount = Number(episode.priorCount);

  return (
    <div
      className={`grid cursor-pointer grid-cols-[3px_1fr_auto] items-start gap-3.5 rounded-[11px] border p-[12px_14px] ${
        isError
          ? "border-[rgba(229,84,74,0.3)] bg-[rgba(229,84,74,0.07)] hover:bg-[rgba(229,84,74,0.11)]"
          : "border-[#22394F] bg-[rgba(201,123,18,0.07)] hover:bg-[rgba(201,123,18,0.11)]"
      }`}
      onClick={() => props.onSelect(episode.id)}
    >
      <span className={`min-h-[44px] w-[3px] self-stretch rounded-[2px] ${severityRailClass(severity)}`} />

      <div className="flex min-w-0 flex-col gap-1.5">
        <div className="flex items-center gap-2.5">
          <StateBadge state="Open" />
          <LiveDuration since={episode.openedAt} className="font-mono text-[11.5px] text-[#DCE8F3]" />
          <span
            className={`whitespace-nowrap rounded-[5px] border px-1.5 text-[10px] ${
              priorCount > 0
                ? "border-[#2C4159] text-[#A9BDD1]"
                : "border-[#3C2A32] text-[#F0685A]"
            }`}
          >
            {priorCount > 0 ? `${ordinal(priorCount + 1)} time` : "first time"}
          </span>
        </div>

        <p className="truncate text-[13.5px] font-medium text-foreground">{episode.firstLogBody}</p>

        <div className="flex min-w-0 items-center gap-2.5 overflow-hidden font-mono text-[11px] whitespace-nowrap text-[#7D93AA]">
          {/* The application name is dropped here on purpose — it is in the rail and the detail. */}
          <span className="flex-none text-[#A9BDD1]">
            {props.known !== undefined ? serviceLabel(props.known.facets) : "—"}
          </span>
          <span className="flex-none">{clock(episode.openedAt)}</span>
          <span className="text-severity-error">{episode.errorCount} err</span>
          {Number(episode.warnCount) > 0 && (
            <span className="text-severity-warn">{episode.warnCount} warn</span>
          )}
        </div>
      </div>

      <div className="flex flex-col items-end gap-[7px]" onClick={event => event.stopPropagation()}>
        {episode.acknowledgedBy !== null ? (
          <span className="rounded-[5px] bg-[#16283C] px-[7px] py-0.5 font-mono text-[10.5px] whitespace-nowrap text-[#A9BDD1]">
            {heldByMe
              ? `you hold this · ${clockShort(episode.acknowledgedAt!)}`
              : `held by ${episode.acknowledgedBy} · ${describeMillis(now - Date.parse(episode.acknowledgedAt!))}`}
          </span>
        ) : (
          <span className="rounded-[5px] border border-dashed border-[#3C2A32] px-[7px] py-0.5 font-mono text-[10.5px] whitespace-nowrap text-[#F0685A]">
            nobody on this yet
          </span>
        )}

        <div className="flex gap-1.5">
          {episode.acknowledgedBy === null ? (
            <Button
              size="sm"
              variant="outline"
              disabled={actions.acknowledge.isPending}
              onClick={() => actions.acknowledge.mutate()}
            >
              Acknowledge
            </Button>
          ) : heldByMe ? (
            <Button
              size="sm"
              variant="outline"
              disabled={actions.withdraw.isPending}
              onClick={() => actions.withdraw.mutate()}
            >
              Withdraw
            </Button>
          ) : (
            <Button
              size="sm"
              variant="outline"
              disabled={actions.acknowledge.isPending}
              onClick={() => actions.acknowledge.mutate()}
            >
              Take over
            </Button>
          )}
          <Button size="sm" onClick={() => setSolveOpen(true)}>
            Solve
          </Button>
        </div>

        {actions.failure != null && (
          <span className="text-[10.5px] text-destructive">{actions.failure.message}</span>
        )}

        <SolveDialog
          episode={episode}
          open={solveOpen}
          onOpenChange={setSolveOpen}
          pending={actions.solve.isPending}
          onSolve={() =>
            actions.solve.mutate(undefined, { onSuccess: () => setSolveOpen(false) })}
        />
      </div>
    </div>
  );
}
