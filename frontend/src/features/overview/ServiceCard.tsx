import { Button } from "@/components/ui/button";
import { LiveDuration } from "@/lib/LiveDuration";
import { severityRailClass } from "@/lib/severity";

import { StatusBadges } from "./badges";
import { MailToggle } from "./MailToggle";
import type { ServiceTile } from "./serviceOverview";
import { Sparkline } from "./Sparkline";
import { clock, windowStamp } from "./stamps";

/**
 * One Service on the board. It never becomes an Episode row: at most one open Episode is quoted
 * (severity rail, meta line, first log body) with nothing to act on — no Acknowledge, no Solve.
 * The moment a reader wants to act, the Alerts page owns that; everything here hands off to
 * Logs and Traces with the service and the window already applied.
 */
export function ServiceCard(props: {
  tile: ServiceTile;
  showVolume: boolean;
  subscribed: boolean;
  viaApplication: boolean;
  onToggleMail: () => void;
  onLogs: () => void;
  onTraces: () => void;
}) {
  const { tile } = props;
  const open = tile.episodes !== undefined && tile.episodes.open > 0;
  const quietedOnly = !open && tile.episodes !== undefined && tile.episodes.quieted > 0;
  const quoted = open ? tile.episodes?.latest : null;
  const logs =
    tile.volume === undefined
      ? undefined
      : tile.volume.error + tile.volume.warn + tile.volume.info + tile.volume.debug;

  return (
    <div
      data-testid="service-card"
      className="relative flex cursor-pointer flex-col rounded-[11px] border border-[#1E344C] bg-card p-3.5 hover:border-[#2A415C]"
      onClick={props.onLogs}
    >
      {/* An overlay ring instead of a changed border, so the layout never shifts. */}
      {open && (
        <div className="pointer-events-none absolute inset-[-1px] rounded-xl border border-[rgba(229,84,74,0.34)] shadow-[inset_3px_0_0_#E5544A]" />
      )}
      {/* Quieted trouble gets the same left rail in the warn tone — settled, but not clear. */}
      {quietedOnly && (
        <div className="pointer-events-none absolute inset-[-1px] rounded-xl border border-[rgba(233,164,60,0.32)] shadow-[inset_3px_0_0_#C97B12]" />
      )}

      <div className="flex items-center gap-[9px]">
        <span className="truncate font-mono text-sm font-medium">{tile.label}</span>
        <span className="ml-auto">
          <MailToggle
            subscribed={props.subscribed}
            viaApplication={props.viaApplication}
            applicationName={tile.applicationName}
            onToggle={props.onToggleMail}
          />
        </span>
      </div>

      <div className="mt-2.5 flex items-center gap-[9px]">
        <StatusBadges tile={tile} />
      </div>

      <div className="mt-[13px] grid grid-cols-3 gap-2">
        <Count caption="ERRORS" value={tile.volume?.error} tone="error" />
        <Count caption="WARN" value={tile.volume?.warn} tone="warn" />
        <Count caption="LOGS" value={logs} tone="neutral" />
      </div>

      {props.showVolume && (
        <>
          <div className="mt-[13px]">
            <Sparkline buckets={tile.volume?.buckets} />
          </div>
          <div className="mt-1 flex justify-between font-mono text-[9.5px] text-[#5F7590]">
            <span>
              {tile.volume?.buckets[0] === undefined ? "" : windowStamp(tile.volume.buckets[0].start)}
            </span>
            <span>now</span>
          </div>
        </>
      )}

      {quoted != null && (
        <div className="mt-3 rounded-lg border border-[rgba(229,84,74,0.2)] bg-[rgba(229,84,74,0.07)] px-[11px] py-[9px]">
          <div className="flex items-center gap-2">
            <span
              className={`h-[15px] w-[3px] shrink-0 rounded-[2px] ${severityRailClass(Number(quoted.firstLogSeverity))}`}
            />
            <span className="truncate font-mono text-[10.5px] text-[#A9BDD1]">
              opened {clock(quoted.openedAt)} · <LiveDuration since={quoted.openedAt} /> ·{" "}
              {Number(quoted.errorCount).toLocaleString()} err
              {Number(quoted.warnCount) > 0 && ` · ${Number(quoted.warnCount).toLocaleString()} warn`}
            </span>
          </div>
          {quoted.firstLogBody != null && (
            <div className="mt-1.5 truncate font-mono text-xs text-[#DCE8F3]">{quoted.firstLogBody}</div>
          )}
        </div>
      )}

      <div className="mt-3 flex items-center gap-2">
        <Button
          variant="ghost"
          size="sm"
          onClick={event => {
            event.stopPropagation();
            props.onLogs();
          }}
        >
          To logs
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={event => {
            event.stopPropagation();
            props.onTraces();
          }}
        >
          To traces
        </Button>
      </div>
    </div>
  );
}

/** A zero renders grey in every band: a grey zero is the point of the tile, a red 0 is a lie. */
function Count(props: { caption: string; value: number | undefined; tone: "error" | "warn" | "neutral" }) {
  const color =
    props.value === undefined || props.value === 0
      ? "text-[#4A6480]"
      : props.tone === "error"
        ? "text-severity-error"
        : props.tone === "warn"
          ? "text-severity-warn"
          : "text-[#B6C8DA]";
  return (
    <div>
      <div className="font-mono text-[9.5px] tracking-[0.12em] text-[#5F7590]">{props.caption}</div>
      <div className={`font-mono text-base leading-[1.1] ${color}`}>
        {props.value === undefined ? "—" : props.value.toLocaleString()}
      </div>
    </div>
  );
}
