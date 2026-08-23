import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

import { api, type Episode, type EpisodeDetail } from "@/api/client";
import { useCurrentUser } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { DetailPanel } from "@/components/ui/detail-panel";
import { Input } from "@/components/ui/input";
import { useLanguage, useT, type Messages } from "@/i18n";
import { canConfigureAlerting } from "@/lib/capabilities";
import { describeMillis } from "@/lib/duration";
import { formatTime } from "@/lib/format";
import { versionAt, type ReleaseTimelines, type VersionAtInstant } from "@/lib/releases";
import { severityLabel } from "@/lib/severity";
import { serviceLabel } from "@/lib/serviceLabel";

import { describeLiveMillis } from "@/lib/duration";
import { LiveDuration, useNow } from "@/lib/LiveDuration";

import { clock, historyStamp } from "./format";
import { GroupingMarks } from "./GroupingMarks";
import { HealthCheckBadge } from "./HealthCheckBadge";
import { describeQuietWindow, MAX_QUIET_WINDOW_MINUTES, quietWindowError } from "./quietWindow";
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

type AlertingWords = Messages["alerting"];

/**
 * The right-hand detail of one Episode: its subject, its lifecycle as a timeline, the volume it
 * burned through, and every earlier Episode of its kind. Same chrome and remembered width as the
 * Logs and Traces detail.
 */
export function EpisodeDetailPanel(props: {
  id: string;
  fromList: Episode | undefined;
  services: Map<string, KnownService>;
  timelines: ReleaseTimelines;
  onClose: () => void;
  onOpenLogs: (episode: Episode) => void;
  onSelectEpisode: (id: string) => void;
}) {
  const t = useT();

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
      if (data === undefined) throw new Error(t.alerting.errors.loadEpisode);
      return data;
    },
    refetchInterval: 30_000,
  });

  const episode = detail.data?.episode ?? props.fromList;

  return (
    <DetailPanel
      title={
        episode === undefined ? (
          <span className={CAPTION}>{t.alerting.detail.episodeCaption}</span>
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
          {detail.data === null ? t.alerting.detail.notVisible : t.common.loading}
        </p>
      ) : (
        <EpisodeBody
          episode={episode}
          detail={detail.data ?? undefined}
          services={props.services}
          version={episode.openedByServiceId === null
            ? undefined
            : versionAt(props.timelines, episode.openedByServiceId, episode.openedAt)}
          onOpenLogs={props.onOpenLogs}
          onSelectEpisode={props.onSelectEpisode}
        />
      )}
    </DetailPanel>
  );
}

function EpisodeBody(props: {
  episode: Episode;
  detail: EpisodeDetail | undefined;
  services: Map<string, KnownService>;
  version: VersionAtInstant | undefined;
  onOpenLogs: (episode: Episode) => void;
  onSelectEpisode: (id: string) => void;
}) {
  const { episode, detail, version } = props;
  const known = episode.openedByServiceId === null
    ? undefined
    : props.services.get(episode.openedByServiceId);
  const t = useT();
  const currentUser = useCurrentUser();
  const actions = useEpisodeActions(episode.id);
  const [solveOpen, setSolveOpen] = useState(false);

  const history = useQuery({
    queryKey: ["alerts", "episode-history", episode.scopeKey, episode.fingerprint],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: { scopeKey: episode.scopeKey, fingerprint: episode.fingerprint, limit: 50 },
        },
      });
      if (error !== undefined) throw new Error(t.alerting.errors.loadHistory);
      return data;
    },
  });

  const isHealthCheck = episode.watch === "HealthCheck";
  const severity = Number(episode.firstMatchSeverity);
  // A watch with no severity bands still reports trouble, and open trouble reads as an error.
  const isError = episode.firstMatchSeverity == null || severity >= 17;
  const myName = currentUser.data?.displayName ?? currentUser.data?.email;
  const heldByMe = episode.acknowledgedBy !== null && episode.acknowledgedBy === myName;
  const earlier = history.data === undefined
    ? Number(episode.priorCount)
    : history.data.items.length - 1;

  // The history arrives newest-first, so its head is the kind's newest episode — the only one
  // the hands land on (ADR 0005). Undefined while loading: the actions wait rather than lie.
  const newestOfKind = history.data?.items[0]?.id;
  const isNewest = newestOfKind === undefined ? undefined : newestOfKind === episode.id;

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
        <p className="text-[13.5px] leading-[1.45] text-foreground">{episode.title}</p>
        <div className="rounded-lg border border-[#1E344C] bg-card px-[11px] py-2.5 font-mono text-[12.5px] leading-[1.55] text-[#DCE8F3]">
          {episode.firstMatchDetail}
        </div>
        <GroupingMarks episode={episode} className="font-mono text-[11px]" />
        <div className="flex items-center gap-2.5">
          {isHealthCheck ? (
            <HealthCheckBadge />
          ) : (
            <span
              className={`rounded px-1.5 font-mono text-[11px] ${
                isError
                  ? "bg-[rgba(229,84,74,0.15)] text-severity-error"
                  : "bg-[rgba(201,123,18,0.15)] text-severity-warn"
              }`}
            >
              {severityLabel(severity)}
            </span>
          )}
          <span className="font-mono text-[11px] text-[#7D93AA]">
            {formatTime(episode.firstMatchAt)}
          </span>
          {/* Nothing was logged, so there is nowhere in the logs to land. */}
          {!isHealthCheck && (
            <button
              type="button"
              className="ml-auto text-[11.5px] whitespace-nowrap text-primary hover:underline"
              onClick={() => props.onOpenLogs(episode)}
            >
              {t.alerting.detail.openInLogsLink}
            </button>
          )}
        </div>
      </div>

      {/* The machine's reading of the evidence — beside it, never above it (CONTEXT.md: Reading). */}
      {detail?.reading != null && <ReadingSection reading={detail.reading} />}

      {/* The machine hand's live marks and the human answers to them (CONTEXT.md: Machine Claim,
          Machine Note, Solved Proposal, Resignation). */}
      <MachineHandSection
        episode={episode}
        actions={actions}
        isNewest={isNewest}
        onConfirm={() => setSolveOpen(true)}
      />


      {/* Lifecycle */}
      <div className="flex flex-col gap-2">
        <p className={CAPTION}>{t.alerting.detail.lifecycleCaption}</p>
        <div className="grid grid-cols-[9px_1fr] gap-x-[11px] gap-y-3">
          {episode.earlierAcknowledgedBy !== null && episode.acknowledgedAt === null && (
            <Moment dot="bg-[#22394F]">
              <p className="text-[12.5px]">
                {episode.earlierAcknowledgedBy === myName
                  ? t.alerting.detail.earlierAckByYou
                  : t.alerting.detail.earlierAckBy(episode.earlierAcknowledgedBy)}
              </p>
              {episode.earlierAcknowledgedAt !== null && (
                <p className="font-mono text-[11px] text-[#7D93AA]">
                  {clock(episode.earlierAcknowledgedAt)}
                </p>
              )}
            </Moment>
          )}

          {/* Before the opening, because it happened before it. Only a Release close enough to be
              worth reading side by side earns a moment of its own; the version itself is on the
              opening line below whether anything was deployed or not (ADR 0016). */}
          {version?.releasedMsBefore !== undefined && (
            <Moment dot="bg-primary">
              <p className="text-[12.5px]">
                {t.alerting.detail.versionReleasedEarlier(
                  <span className="text-primary">{version.version}</span>,
                  describeMillis(version.releasedMsBefore),
                )}
              </p>
              <p className="font-mono text-[11px] text-[#7D93AA]">
                {clock(new Date(Date.parse(episode.openedAt) - version.releasedMsBefore).toISOString())}
              </p>
            </Moment>
          )}

          <Moment dot={isError ? "bg-severity-error-rail" : "bg-severity-warn-rail"}>
            <p className="text-[12.5px]">
              {isHealthCheck
                ? t.alerting.detail.openedByHealthCheck
                : t.alerting.detail.openedByLog(severityLabel(severity))}
            </p>
            <p className="font-mono text-[11px] text-[#7D93AA]">
              {clock(episode.openedAt)}
              {/* Sensitivity is the logs watch's setting and governs nothing here. */}
              {!isHealthCheck && detail !== undefined
                && ` · ${t.alerting.detail.sensitivity(
                  sensitivityWords(t.alerting, detail.effectiveSensitivity),
                )}`}
            </p>
          </Moment>

          {detail?.mailAlert != null && (
            <Moment dot="bg-[#22394F]">
              <p className="text-[12.5px]">
                {t.alerting.detail.alertMailed(Number(detail.mailAlert.subscriberCount))}
              </p>
              <p className="font-mono text-[11px] text-[#7D93AA]">
                {detail.mailAlert.firstDeliveredAt != null
                  ? clock(detail.mailAlert.firstDeliveredAt)
                  : t.alerting.detail.deliveryPending}
                {detail.chatAlert != null && ` · ${t.alerting.detail.postedToChat}`}
              </p>
            </Moment>
          )}
          {detail?.mailAlert == null && detail?.chatAlert != null && (
            <Moment dot="bg-[#22394F]">
              <p className="text-[12.5px]">{t.alerting.detail.alertPostedToChat}</p>
              <p className="font-mono text-[11px] text-[#7D93AA]">
                {detail.chatAlert.deliveredAt != null
                  ? clock(detail.chatAlert.deliveredAt)
                  : t.alerting.detail.deliveryPending}
              </p>
            </Moment>
          )}

          {detail !== undefined && <JournalMoments journal={detail.journal} myName={myName} />}

          {episode.state === "Open" && (
            <StillMatching
              lastMatchAt={episode.lastMatchAt}
              acknowledged={episode.acknowledgedAt !== null}
              machineClaimed={episode.machineClaim != null}
              quietWindowMinutes={detail === undefined ? undefined : Number(detail.quietWindowMinutes)}
              healthCheck={isHealthCheck}
            />
          )}
        </div>
      </div>

      {/* Volume so far — only where the watch counts anything. The health check watch produces
          one failed probe per beat, which measures the beat rather than the trouble. */}
      {!isHealthCheck && (
        <div className="flex flex-col gap-2">
          <p className={CAPTION}>{t.alerting.detail.volumeCaption}</p>
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
              {t.alerting.detail.errorsCount(errorCount)}
            </span>
            {warnCount > 0 && (
              <span className="whitespace-nowrap text-severity-warn">
                {t.alerting.detail.warningsCount(warnCount)}
              </span>
            )}
            <span className="ml-auto text-[#7D93AA]">≈ {ratePerMinute(episode)} / min</span>
          </div>
        </div>
      )}

      <ParticipationsSection episode={episode} services={props.services} />

      {/* Recurrence */}
      <div className="flex flex-col gap-2">
        <p className={CAPTION}>
          {earlier === 0
            ? t.alerting.detail.kindFirstCaption
            : t.alerting.detail.kindEarlierCaption(earlier)}
        </p>
        <div>
          {(history.data?.items ?? []).map(item => (
            <div
              key={item.id}
              className="grid grid-cols-[1fr_62px_1fr] items-center border-t border-[#101F31] py-[5px] font-mono text-[11px] text-[#7D93AA]"
            >
              <span className="whitespace-nowrap">{historyStamp(item.openedAt, Date.now())}</span>
              <span className={STATE_TEXT[item.state]}>{t.alerting.state.badge[item.state]}</span>
              <span className="truncate text-right">
                {item.id === episode.id
                  ? t.alerting.detail.thisOne
                  : item.solvedBy !== null
                    ? t.alerting.detail.byName(item.solvedBy)
                    : item.acknowledgedBy !== null
                      ? t.alerting.detail.byName(item.acknowledgedBy)
                      : t.alerting.detail.nobody}
              </span>
            </div>
          ))}
          {history.isPending && (
            <p className="py-2 font-mono text-[11px] text-[#5F7590]">{t.common.loading}</p>
          )}
        </div>
        <p className="text-[11.5px] text-[#6E86A0]">
          {earlier === 0
            ? t.alerting.detail.firstOfKind
            : t.alerting.detail.cameBack(earlier)}
        </p>
      </div>

      {detail !== undefined && (
        <QuietWindowSection
          episodeId={episode.id}
          own={episode.fingerprintQuietWindowMinutes == null
            ? null
            : Number(episode.fingerprintQuietWindowMinutes)}
          inherited={Number(detail.inheritedQuietWindowMinutes)}
          editable={canConfigureAlerting(currentUser.data)}
        />
      )}

      {/* Actions — the hands land only on the kind's newest episode (ADR 0005). */}
      <div className="sticky -bottom-4 -mx-5 mt-auto flex items-center gap-2 border-t border-[#17293D] bg-[#0B1826] px-[18px] py-3">
        {isNewest === false && (
          <span className="text-[11.5px] text-[#8CA1B8]">
            {t.alerting.detail.isHistory}{" "}
            <button
              type="button"
              className="cursor-pointer whitespace-nowrap text-primary hover:underline"
              onClick={() => props.onSelectEpisode(newestOfKind!)}
            >
              {t.alerting.detail.openIt}
            </button>
          </span>
        )}
        {episode.state !== "Solved" && isNewest === true && (
          <>
            <Button size="sm" onClick={() => setSolveOpen(true)}>
              {t.alerting.actions.solve}
            </Button>
            {episode.acknowledgedBy === null ? (
              <Button
                size="sm"
                variant="outline"
                disabled={actions.acknowledge.isPending}
                onClick={() => actions.acknowledge.mutate()}
              >
                {t.alerting.actions.acknowledge}
              </Button>
            ) : heldByMe ? (
              <Button
                size="sm"
                variant="outline"
                disabled={actions.withdraw.isPending}
                onClick={() => actions.withdraw.mutate()}
              >
                {t.alerting.actions.withdraw}
              </Button>
            ) : (
              <Button
                size="sm"
                variant="outline"
                disabled={actions.acknowledge.isPending}
                onClick={() => actions.acknowledge.mutate()}
              >
                {t.alerting.actions.takeOver}
              </Button>
            )}
          </>
        )}
        {!isHealthCheck && (
          <Button size="sm" variant="ghost" onClick={() => props.onOpenLogs(episode)}>
            {t.alerting.actions.openInLogs}
          </Button>
        )}
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

/**
 * The machine hand on this Episode (Alerting CONTEXT.md): the claim that holds it, the pinned
 * note, the Solved Proposal awaiting a verdict, the Resignation calling for a human hand — and
 * the buttons that answer them. Confirming the proposal is the Solve itself, so it opens the
 * same dialog; nothing here renders anything on its own. Absent entirely while no mark stands.
 */
function MachineHandSection(props: {
  episode: Episode;
  actions: ReturnType<typeof useEpisodeActions>;
  isNewest: boolean | undefined;
  onConfirm: () => void;
}) {
  const t = useT();
  const { episode, actions } = props;
  const claim = episode.machineClaim;
  const note = episode.machineNote;
  const proposal = episode.solvedProposal;
  const resignation = episode.resignation;

  if (claim == null && note == null && proposal == null && resignation == null) {
    return null;
  }

  const handOf = (by: { name: string | null; holderEmail: string | null }) =>
    by.name === null
      ? t.alerting.machine.formerHand
      : t.alerting.machine.hand(by.name, by.holderEmail);

  return (
    <div className="flex flex-col gap-2.5">
      <p className={CAPTION}>{t.alerting.machine.caption}</p>

      {claim != null && (
        <div className="flex items-center gap-2.5">
          <div className="flex min-w-0 flex-1 flex-col gap-0.5">
            <p className="text-[12.5px]">{t.alerting.machine.claimHeld(handOf(claim.by))}</p>
            <p className="font-mono text-[11px] text-[#7D93AA]">
              {t.alerting.machine.leaseUntil(clock(claim.leaseUntil))}
            </p>
          </div>
          <Button
            size="sm"
            variant="outline"
            disabled={actions.withdrawClaim.isPending}
            onClick={() => actions.withdrawClaim.mutate()}
          >
            {t.alerting.machine.withdrawClaim}
          </Button>
        </div>
      )}

      {note != null && (
        <div className="flex flex-col gap-1 rounded-lg border border-[#1E344C] bg-card px-[11px] py-2.5">
          <p className="font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">
            {t.alerting.machine.noteCaption} · {clock(note.at)}
          </p>
          {note.text != null && (
            <p className="text-[12.5px] leading-[1.55] text-[#DCE8F3]">{note.text}</p>
          )}
          {note.link != null && (
            <a
              className="truncate text-[11.5px] text-primary hover:underline"
              href={note.link}
              target="_blank"
              rel="noreferrer"
            >
              {t.alerting.machine.openLink}
            </a>
          )}
        </div>
      )}

      {proposal != null && (
        <div className="flex flex-col gap-1.5 rounded-lg border border-[rgba(233,164,60,0.45)] bg-[rgba(233,164,60,0.07)] px-[11px] py-2.5">
          <p className="text-[12.5px] font-medium text-[#DCE8F3]">
            {t.alerting.machine.proposalHeading}
          </p>
          <p className="text-[11.5px] text-[#8CA1B8]">
            {t.alerting.machine.proposalLaidBy(handOf(proposal.by))} · {clock(proposal.at)}
          </p>
          <p className="font-mono text-[11px] text-[#A9BDD1]">
            {t.alerting.machine.matchesSince(Number(proposal.matchesSince))}
          </p>
          {proposal.link != null && (
            <a
              className="truncate text-[11.5px] text-primary hover:underline"
              href={proposal.link}
              target="_blank"
              rel="noreferrer"
            >
              {t.alerting.machine.openPr}
            </a>
          )}
          {proposal.overtaken ? (
            <p className="text-[11.5px] text-[#8CA1B8]">{t.alerting.machine.overtakenNote}</p>
          ) : (
            <div className="mt-1 flex items-center gap-2">
              <Button
                size="sm"
                disabled={props.isNewest !== true || episode.state === "Solved"}
                onClick={props.onConfirm}
              >
                {t.alerting.machine.confirmSolved}
              </Button>
              <Button
                size="sm"
                variant="outline"
                disabled={actions.rejectProposal.isPending}
                onClick={() => actions.rejectProposal.mutate()}
              >
                {t.alerting.machine.reject}
              </Button>
            </div>
          )}
        </div>
      )}

      {resignation != null && (
        <div className="flex flex-col gap-1.5 rounded-lg border border-[rgba(201,123,18,0.45)] bg-[rgba(201,123,18,0.07)] px-[11px] py-2.5">
          <p className="text-[12.5px] font-medium text-[#DCE8F3]">
            {t.alerting.machine.resignationHeading}
          </p>
          <p className="text-[12.5px] leading-[1.55] text-[#DCE8F3]">{resignation.reason}</p>
          <p className="text-[11.5px] text-[#8CA1B8]">
            {t.alerting.machine.resignedBy(handOf(resignation.by))} · {clock(resignation.at)}
          </p>
          {resignation.overtaken ? (
            <p className="text-[11.5px] text-[#8CA1B8]">
              {t.alerting.machine.resignationOvertakenNote}
            </p>
          ) : null}
          <div className="mt-1">
            <Button
              size="sm"
              variant="outline"
              disabled={actions.dismissResignation.isPending}
              onClick={() => actions.dismissResignation.mutate()}
            >
              {t.alerting.machine.dismiss}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * The machine's reading of the opening evidence: visibly machine-made (the model is named), in
 * the viewer's own language, and carrying no authority — Solved stays a human verdict. Pending
 * and failed states are said quietly; the evidence never depends on this card.
 */
function ReadingSection(props: { reading: NonNullable<EpisodeDetail["reading"]> }) {
  const t = useT();
  const language = useLanguage();
  const { reading } = props;

  return (
    <div className="flex flex-col gap-2">
      <p className={CAPTION}>{t.alerting.reading.caption}</p>
      {reading.state === "Written" ? (
        <div className="flex flex-col gap-1.5 rounded-lg border border-[rgba(178,110,14,0.45)] bg-[rgba(178,110,14,0.08)] px-[11px] py-2.5">
          <p className="text-[12.5px] leading-[1.55] text-[#DCE8F3]">
            {language === "cs" ? reading.textCs : reading.textEn}
          </p>
          {reading.model != null && (
            <p className="text-[11px] text-[#7D93AA]">{t.alerting.reading.writtenBy(reading.model)}</p>
          )}
        </div>
      ) : (
        <p className="text-[12px] text-[#7D93AA]">
          {reading.state === "Pending" ? t.alerting.reading.pending : t.alerting.reading.failed}
        </p>
      )}
    </div>
  );
}

/**
 * The Quiet Window this kind of trouble keeps for itself (ADR 0004). It is deliberately not
 * worded as a property of the Episode: what is saved here outlives it and governs every later
 * Episode of the same kind — which is why the field stays live on a closed one too.
 */
/**
 * Which Services and versions are in this Episode (see CONTEXT.md: Participation) — when each
 * first and last fell in, and how much it put in. The read this table exists for is "is it still
 * happening on the version we just shipped, and is it every deployment or only one", so the
 * version comes from each Match's own `service.version` rather than from the Release ledger,
 * which reports one version during a rolling deploy while two are demonstrably running (ADR 0016).
 */
function ParticipationsSection(props: {
  episode: Episode;
  services: Map<string, KnownService>;
}) {
  const t = useT();
  const { participations } = props.episode;
  if (participations.length === 0) return null;

  return (
    <div className="flex flex-col gap-2">
      <p className={CAPTION}>{t.alerting.participants.caption(participations.length)}</p>
      <div>
        <div className="grid grid-cols-[1fr_86px_62px_54px] items-center gap-2 border-b border-[#17293D] pb-[5px] font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">
          <span>{t.alerting.participants.columnService}</span>
          <span>{t.alerting.participants.columnVersion}</span>
          <span>{t.alerting.participants.columnLast}</span>
          <span className="text-right">{t.alerting.participants.columnMatches}</span>
        </div>
        {participations.map(participation => {
          const known = props.services.get(participation.serviceId);
          const matches = Number(participation.errorCount) + Number(participation.warnCount);
          return (
            <div
              key={`${participation.serviceId} ${participation.version ?? ""}`}
              className="grid grid-cols-[1fr_86px_62px_54px] items-center gap-2 border-t border-[#101F31] py-[5px] font-mono text-[11px] text-[#7D93AA]"
              title={t.alerting.participants.firstSeen(clock(participation.firstAt))}
            >
              <span className="truncate text-[#A9BDD1]">
                {known === undefined ? "—" : serviceLabel(known.facets)}
              </span>
              <span className="truncate text-[#DCE8F3]">
                {participation.version ?? t.alerting.participants.noVersion}
              </span>
              <span className="whitespace-nowrap">
                {historyStamp(participation.lastAt, Date.now())}
              </span>
              <span className="text-right">{matches}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function QuietWindowSection(props: {
  episodeId: string;
  own: number | null;
  inherited: number;
  editable: boolean;
}) {
  const t = useT();
  const queryClient = useQueryClient();
  const save = useMutation({
    mutationFn: async (minutes: number | null) => {
      const { response } = await api.PUT("/api/admin/episodes/{id}/quiet-window", {
        params: { path: { id: props.episodeId } },
        body: { quietWindowMinutes: minutes },
      });
      if (!response.ok) throw new Error(t.alerting.quietWindow.notSaved);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["alerts"] }),
  });

  // Committed on blur and on Enter, like every other settings field here. A value the API would
  // refuse simply does not commit: an uncommitted field is clearer than an error.
  const commit = (raw: string) => {
    if (quietWindowError(raw) !== null) {
      return;
    }

    const trimmed = raw.trim();
    const next = trimmed.length === 0 ? null : Number(trimmed);
    if (next !== props.own) {
      save.mutate(next);
    }
  };

  return (
    <div className="flex flex-col gap-2">
      <p className={CAPTION}>{t.alerting.quietWindow.caption}</p>
      {props.editable && (
        <div className="flex items-center gap-2">
          <Input
            aria-label={t.alerting.quietWindow.fieldLabel}
            // Keyed on the saved value so a refetch — or another episode — reloads the field.
            key={`${props.episodeId}:${props.own ?? ""}`}
            className="h-8 w-[104px] font-mono text-[12px]"
            type="number"
            min={1}
            max={MAX_QUIET_WINDOW_MINUTES}
            placeholder={String(props.inherited)}
            defaultValue={props.own ?? ""}
            disabled={save.isPending}
            onBlur={event => commit(event.currentTarget.value)}
            onKeyDown={event => {
              if (event.key === "Enter") {
                event.preventDefault();
                commit(event.currentTarget.value);
              }
            }}
          />
          <span className="font-mono text-[11px] text-[#7D93AA]">
            {t.alerting.quietWindow.emptyInherits}
          </span>
        </div>
      )}
      <p className="text-[11.5px] text-[#6E86A0]">
        {describeQuietWindow({ own: props.own, inherited: props.inherited })}
      </p>
      {save.error != null && (
        <p className="text-[11.5px] text-destructive">{save.error.message}</p>
      )}
    </div>
  );
}

/**
 * The human hands on the timeline, read from the Journal (ADR 0006) — every act kept, nothing
 * lost. A take-over and a withdrawal are narrated from the sequence itself: the entries carry
 * only which hand, whose, and when.
 */
function JournalMoments(props: {
  journal: EpisodeDetail["journal"];
  myName: string | undefined;
}) {
  const words = useT().alerting.journal;

  // Whose acknowledgement is live at this point of the story — what turns the next
  // "acknowledged" into a take-over and names whose mark a withdrawal ended.
  let held = false;
  let holder: string | null = null;

  return props.journal.map((entry, index) => {
    // The viewer's own hand speaks in its own person; a deleted account keeps a stand-in name.
    const isMe = entry.by !== null && entry.by === props.myName;
    const name = entry.by ?? words.formerUser;

    // The machine hand's entries: narrated by the delegation's name, and where a person acted
    // over a machine's mark, the entry names both hands.
    const machine = entry.machine?.name ?? words.formerMachine;
    const machineText: Partial<Record<typeof entry.kind, string>> = {
      Claimed: words.claimed(machine),
      ClaimRenewed: words.claimRenewed(machine),
      ClaimReleased: words.claimReleased(machine),
      ClaimLapsed: words.claimLapsed(machine),
      ClaimDisplaced: words.claimDisplaced(name, machine),
      NotePinned: words.notePinned(machine),
      ProposalLaid: words.proposalLaid(machine),
      ProposalRejected: words.proposalRejected(name, machine),
      Resigned: words.resigned(machine),
      ResignationDismissed: words.resignationDismissed(name, machine),
    };
    const machineNarration = machineText[entry.kind];
    if (machineNarration !== undefined) {
      const machineDot =
        entry.kind === "Resigned"
          ? "bg-severity-warn-rail"
          : entry.kind === "ProposalLaid"
            ? "bg-primary"
            : "bg-[#22394F]";
      return (
        <Moment key={index} dot={machineDot}>
          <p className="text-[12.5px]">{machineNarration}</p>
          <p className="font-mono text-[11px] text-[#7D93AA]">{clock(entry.at)}</p>
        </Moment>
      );
    }

    let text: string;
    let dot: string;
    if (entry.kind === "Acknowledged") {
      text = held
        ? isMe ? words.youTookOver : words.tookOver(name)
        : isMe ? words.youAcknowledged : words.acknowledged(name);
      dot = "bg-primary";
      held = true;
      holder = entry.by;
    } else if (entry.kind === "Withdrawn") {
      const own = entry.by !== null && entry.by === holder;
      text = own
        ? isMe ? words.youWithdrewYours : words.withdrewTheirOwn(entry.by!)
        : holder === null
          ? isMe ? words.youWithdrewThe : words.withdrewThe(name)
          : holder === props.myName
            ? words.withdrewYours(name)
            : isMe
              ? words.youWithdrewOf(holder)
              : words.withdrewOf(name, holder);
      dot = "bg-[#22394F]";
      held = false;
      holder = null;
    } else {
      text = isMe ? words.solvedByYou : words.solvedBy(name);
      dot = "bg-state-solved";
      held = false;
      holder = null;
    }

    return (
      <Moment key={index} dot={dot}>
        <p className="text-[12.5px]">{text}</p>
        <p className="font-mono text-[11px] text-[#7D93AA]">{clock(entry.at)}</p>
      </Moment>
    );
  });
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
function StillMatching(props: {
  lastMatchAt: string;
  acknowledged: boolean;
  machineClaimed: boolean;
  quietWindowMinutes: number | undefined;
  healthCheck: boolean;
}) {
  const t = useT();
  const now = useNow();
  const since = describeLiveMillis(now - Date.parse(props.lastMatchAt));
  return (
    <Moment dot="bg-severity-error-rail" pulse>
      <p className="text-[12.5px]">
        {props.healthCheck
          ? t.alerting.detail.stillMatchingCheck(since)
          : t.alerting.detail.stillMatchingLog(since)}
      </p>
      {props.acknowledged ? (
        <p className="font-mono text-[11px] text-[#7D93AA]">{t.alerting.detail.heldOpenNote}</p>
      ) : props.machineClaimed ? (
        // The same hold on a lease (CONTEXT.md: Machine Claim) — said in the machine's terms.
        <p className="font-mono text-[11px] text-[#7D93AA]">{t.alerting.machine.heldOpenNote}</p>
      ) : (
        props.quietWindowMinutes !== undefined && (
          <p className="font-mono text-[11px] text-[#7D93AA]">
            {t.alerting.detail.autoCloseNote(props.quietWindowMinutes)}
          </p>
        )
      )}
    </Moment>
  );
}

function sensitivityWords(
  words: AlertingWords,
  sensitivity: EpisodeDetail["effectiveSensitivity"],
): string {
  return sensitivity === "ErrorsAndWarnings"
    ? words.detail.sensitivityWords.errorsAndWarnings
    : sensitivity === "Errors"
      ? words.detail.sensitivityWords.errors
      : words.detail.sensitivityWords.off;
}

function ratePerMinute(episode: Episode): string {
  const end = episode.closedAt != null ? Date.parse(episode.closedAt) : Date.now();
  const minutes = Math.max(1, (end - Date.parse(episode.openedAt)) / 60_000);
  const rate = (Number(episode.errorCount) + Number(episode.warnCount)) / minutes;
  return rate < 1 ? "<1" : String(Math.round(rate));
}
