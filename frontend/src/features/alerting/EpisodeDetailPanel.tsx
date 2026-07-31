import { useQuery } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

import { api, type Episode, type EpisodeDetail } from "@/api/client";
import { useCurrentUser } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { DetailPanel } from "@/components/ui/detail-panel";
import { describeMillis } from "@/lib/duration";
import { formatTime } from "@/lib/format";
import { severityLabel } from "@/lib/severity";
import { serviceLabel } from "@/lib/serviceLabel";

import { clock, describeLiveMillis, historyStamp } from "./format";
import { LiveDuration, useNow } from "./LiveDuration";
import { SolveDialog } from "./SolveDialog";
import type { KnownService } from "./serviceIndex";
import { StateBadge } from "./StateBadge";
import { useEpisodeActions } from "./useEpisodeActions";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

const STATE_TEXT: Record<Episode["state"], string> = {
  Open: "text-severity-error",
  Quieted: "text-severity-warn",
  Solved: "text-state-solved",
  Muted: "text-[#6E86A0]",
};

/**
 * The right-hand detail of one Episode: its subject, its lifecycle as a timeline, the volume it
 * burned through, and every earlier Episode of its kind. Same chrome and remembered width as the
 * Logs and Traces detail.
 */
export function EpisodeDetailPanel(props: {
  id: string;
  fromList: Episode | undefined;
  services: Map<string, KnownService>;
  onClose: () => void;
  onOpenLogs: (episode: Episode) => void;
}) {
  // The detail is fetched even when the list row is at hand: the timeline needs the deliveries
  // and the effective settings, which no list row carries. A URL-selected episode outside the
  // loaded pages arrives the same way.
  const detail = useQuery({
    queryKey: ["alerts", "episode-detail", props.id],
    queryFn: async () => {
      const { data, response } = await api.GET("/api/alerting/episodes/{id}/detail", {
        params: { path: { id: props.id } },
      });
      if (response.status === 404) return null;
      if (data === undefined) throw new Error("Failed to load the episode");
      return data;
    },
    refetchInterval: 30_000,
  });

  const episode = detail.data?.episode ?? props.fromList;

  return (
    <DetailPanel
      title={
        episode === undefined ? (
          <span className={CAPTION}>EPISODE</span>
        ) : (
          <span className="flex items-center gap-2">
            <StateBadge state={episode.state} />
            <span className="font-mono text-[10.5px] tracking-[0.08em] text-[#7D93AA] uppercase">
              ·{" "}
              {episode.closedAt == null
                ? <LiveDuration since={episode.openedAt} />
                : describeMillis(Date.parse(episode.closedAt) - Date.parse(episode.openedAt))}
            </span>
          </span>
        )
      }
      onClose={props.onClose}
    >
      {episode === undefined ? (
        <p className="text-[12.5px] text-[#8CA1B8]">
          {detail.data === null
            ? "This episode is not visible to you — or it no longer exists."
            : "Loading…"}
        </p>
      ) : (
        <EpisodeBody
          episode={episode}
          detail={detail.data ?? undefined}
          known={props.services.get(episode.serviceId)}
          onOpenLogs={props.onOpenLogs}
        />
      )}
    </DetailPanel>
  );
}

function EpisodeBody(props: {
  episode: Episode;
  detail: EpisodeDetail | undefined;
  known: KnownService | undefined;
  onOpenLogs: (episode: Episode) => void;
}) {
  const { episode, detail, known } = props;
  const currentUser = useCurrentUser();
  const actions = useEpisodeActions(episode.id);
  const [solveOpen, setSolveOpen] = useState(false);

  const history = useQuery({
    queryKey: ["alerts", "episode-history", episode.serviceId, episode.fingerprint],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: { serviceId: [episode.serviceId], fingerprint: episode.fingerprint, limit: 50 },
        },
      });
      if (error !== undefined) throw new Error("Failed to load the history");
      return data;
    },
  });

  const severity = Number(episode.firstLogSeverity);
  const isError = severity >= 17;
  const myName = currentUser.data?.displayName ?? currentUser.data?.email;
  const heldByMe = episode.acknowledgedBy !== null && episode.acknowledgedBy === myName;
  const earlier = history.data === undefined
    ? Number(episode.priorCount)
    : history.data.items.length - 1;

  const errorCount = Number(episode.errorCount);
  const warnCount = Number(episode.warnCount);
  const totalCount = errorCount + warnCount;

  return (
    <>
      {/* Subject */}
      <div className="flex flex-col gap-2.5">
        <p className="font-mono text-[11.5px] text-[#A9BDD1]">
          {known !== undefined
            ? `${known.application.name} · ${serviceLabel(known.facets)}`
            : "—"}
        </p>
        <div className="rounded-lg border border-[#1E344C] bg-card px-[11px] py-2.5 font-mono text-[12.5px] leading-[1.55] text-[#DCE8F3]">
          {episode.firstLogBody}
        </div>
        <div className="flex items-center gap-2.5">
          <span
            className={`rounded px-1.5 font-mono text-[11px] ${
              isError
                ? "bg-[rgba(229,84,74,0.15)] text-severity-error"
                : "bg-[rgba(201,123,18,0.15)] text-severity-warn"
            }`}
          >
            {severityLabel(severity)}
          </span>
          <span className="font-mono text-[11px] text-[#7D93AA]">
            {formatTime(episode.firstLogTimestamp)}
          </span>
          <button
            type="button"
            className="ml-auto text-[11.5px] whitespace-nowrap text-primary hover:underline"
            onClick={() => props.onOpenLogs(episode)}
          >
            Open in logs ›
          </button>
        </div>
      </div>

      {/* Lifecycle */}
      <div className="flex flex-col gap-2">
        <p className={CAPTION}>LIFECYCLE</p>
        <div className="grid grid-cols-[9px_1fr] gap-x-[11px] gap-y-3">
          <Moment dot={isError ? "bg-severity-error-rail" : "bg-severity-warn-rail"}>
            <p className="text-[12.5px]">Opened by an {severityLabel(severity)} log</p>
            <p className="font-mono text-[11px] text-[#7D93AA]">
              {clock(episode.openedAt)}
              {detail !== undefined && ` · sensitivity ${sensitivityWords(detail.effectiveSensitivity)}`}
            </p>
          </Moment>

          {detail?.mailAlert != null && (
            <Moment dot="bg-[#22394F]">
              <p className="text-[12.5px]">
                Alert mailed to {detail.mailAlert.subscriberCount} subscriber
                {Number(detail.mailAlert.subscriberCount) === 1 ? "" : "s"}
              </p>
              <p className="font-mono text-[11px] text-[#7D93AA]">
                {detail.mailAlert.firstDeliveredAt != null
                  ? clock(detail.mailAlert.firstDeliveredAt)
                  : "delivery pending"}
                {detail.chatAlert != null && " · posted to Google Chat"}
              </p>
            </Moment>
          )}
          {detail?.mailAlert == null && detail?.chatAlert != null && (
            <Moment dot="bg-[#22394F]">
              <p className="text-[12.5px]">Alert posted to Google Chat</p>
              <p className="font-mono text-[11px] text-[#7D93AA]">
                {detail.chatAlert.deliveredAt != null
                  ? clock(detail.chatAlert.deliveredAt)
                  : "delivery pending"}
              </p>
            </Moment>
          )}

          {episode.acknowledgedAt !== null && (
            <Moment dot="bg-primary">
              <p className="text-[12.5px]">
                {heldByMe
                  ? "You acknowledged it"
                  : `Acknowledged by ${episode.acknowledgedBy ?? "a user no longer here"}`}
              </p>
              <p className="font-mono text-[11px] text-[#7D93AA]">
                {clock(episode.acknowledgedAt)}
                {heldByMe && currentUser.data?.email !== undefined && ` · ${currentUser.data.email}`}
              </p>
            </Moment>
          )}

          {episode.solvedAt !== null && (
            <Moment dot="bg-state-solved">
              <p className="text-[12.5px]">
                Solved by {episode.solvedBy ?? "a user no longer here"}
              </p>
              <p className="font-mono text-[11px] text-[#7D93AA]">{clock(episode.solvedAt)}</p>
            </Moment>
          )}

          {episode.state === "Open" && (
            <StillMatching
              lastMatchAt={episode.lastMatchAt}
              quietWindowMinutes={detail === undefined ? undefined : Number(detail.quietWindowMinutes)}
            />
          )}
        </div>
      </div>

      {/* Volume so far */}
      <div className="flex flex-col gap-2">
        <p className={CAPTION}>VOLUME SO FAR</p>
        <div className="flex h-2 overflow-hidden rounded bg-[#101F31]">
          {totalCount > 0 && (
            <>
              <span
                style={{
                  width: `${(errorCount / totalCount) * 100}%`,
                  background: "var(--severity-error-fill)",
                }}
              />
              <span
                style={{
                  width: `${(warnCount / totalCount) * 100}%`,
                  background: "var(--severity-warn-fill)",
                }}
              />
            </>
          )}
        </div>
        <div className="flex items-center gap-3 font-mono text-[11px]">
          <span className="whitespace-nowrap text-severity-error">
            {errorCount} error{errorCount === 1 ? "" : "s"}
          </span>
          {warnCount > 0 && (
            <span className="whitespace-nowrap text-severity-warn">
              {warnCount} warning{warnCount === 1 ? "" : "s"}
            </span>
          )}
          <span className="ml-auto text-[#7D93AA]">≈ {ratePerMinute(episode)} / min</span>
        </div>
      </div>

      {/* Recurrence */}
      <div className="flex flex-col gap-2">
        <p className={CAPTION}>
          THIS KIND OF TROUBLE · {earlier === 0 ? "FIRST TIME" : `${earlier} EARLIER`}
        </p>
        <div>
          {(history.data?.items ?? []).map(item => (
            <div
              key={item.id}
              className="grid grid-cols-[1fr_62px_1fr] items-center border-t border-[#101F31] py-[5px] font-mono text-[11px] text-[#7D93AA]"
            >
              <span className="whitespace-nowrap">{historyStamp(item.openedAt, Date.now())}</span>
              <span className={STATE_TEXT[item.state]}>{item.state.toLowerCase()}</span>
              <span className="truncate text-right">
                {item.id === episode.id
                  ? "← this one"
                  : item.solvedBy !== null
                    ? `by ${item.solvedBy}`
                    : item.acknowledgedBy !== null
                      ? `by ${item.acknowledgedBy}`
                      : "nobody"}
              </span>
            </div>
          ))}
          {history.isPending && (
            <p className="py-2 font-mono text-[11px] text-[#5F7590]">Loading…</p>
          )}
        </div>
        <p className="text-[11.5px] text-[#6E86A0]">
          {earlier === 0
            ? "The first episode of its kind in this service."
            : `Same service, same kind of trouble — it came back ${
              earlier === 1 ? "once" : `${earlier} times`
            } before.`}
        </p>
      </div>

      {/* Actions */}
      <div className="sticky -bottom-4 -mx-5 mt-auto flex items-center gap-2 border-t border-[#17293D] bg-[#0B1826] px-[18px] py-3">
        {episode.state !== "Solved" && (
          <>
            <Button size="sm" onClick={() => setSolveOpen(true)}>
              Solve
            </Button>
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
          </>
        )}
        <Button size="sm" variant="ghost" onClick={() => props.onOpenLogs(episode)}>
          Open in logs
        </Button>
        {actions.failure != null && (
          <span className="text-[11.5px] text-destructive">{actions.failure.message}</span>
        )}
      </div>

      <SolveDialog
        episode={episode}
        open={solveOpen}
        onOpenChange={setSolveOpen}
        pending={actions.solve.isPending}
        onSolve={() => actions.solve.mutate(undefined, { onSuccess: () => setSolveOpen(false) })}
      />
    </>
  );
}

/** One dot and its two lines on the lifecycle timeline. */
function Moment(props: { dot: string; pulse?: boolean; children: ReactNode }) {
  return (
    <>
      <span
        className={`mt-[5px] size-[7px] rounded-full ${props.dot} ${
          props.pulse === true ? "animate-[bpulse_1.6s_ease-in-out_infinite]" : ""
        }`}
      />
      <div className="flex min-w-0 flex-col gap-0.5">{props.children}</div>
    </>
  );
}

/** The open episode's living tail: what makes Quieted comprehensible. Ticks on the shared clock. */
function StillMatching(props: { lastMatchAt: string; quietWindowMinutes: number | undefined }) {
  const now = useNow();
  return (
    <Moment dot="bg-severity-error-rail" pulse>
      <p className="text-[12.5px]">
        Still matching — last log {describeLiveMillis(now - Date.parse(props.lastMatchAt))} ago
      </p>
      {props.quietWindowMinutes !== undefined && (
        <p className="font-mono text-[11px] text-[#7D93AA]">
          closes on its own after {props.quietWindowMinutes} min of quiet
        </p>
      )}
    </Moment>
  );
}

function sensitivityWords(sensitivity: EpisodeDetail["effectiveSensitivity"]): string {
  return sensitivity === "ErrorsAndWarnings"
    ? "errors + warnings"
    : sensitivity === "Errors" ? "errors" : "off";
}

function ratePerMinute(episode: Episode): string {
  const end = episode.closedAt != null ? Date.parse(episode.closedAt) : Date.now();
  const minutes = Math.max(1, (end - Date.parse(episode.openedAt)) / 60_000);
  const rate = (Number(episode.errorCount) + Number(episode.warnCount)) / minutes;
  return rate < 1 ? "<1" : String(Math.round(rate));
}
