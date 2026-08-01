import { describeMillis } from "@/lib/duration";
import { LiveDuration } from "@/lib/LiveDuration";
import { severityRailClass } from "@/lib/severity";

import { StatusBadges } from "./badges";
import { MailToggle } from "./MailToggle";
import type { ServiceTile } from "./serviceOverview";
import { Sparkline } from "./Sparkline";

/**
 * The same services, one line each, for a reader who owns forty of them. No grouping and no
 * section headings by design — the sort does the work, and grouping a table means either
 * repeated headers or a second scroll context.
 *
 * A real table, not a grid: auto layout sizes every column to its widest value across all rows,
 * so counts and badges are never clipped and never wrapped. The one flexible column is LATEST
 * EPISODE — free log text is the only content allowed to truncate.
 */
export function ServiceTable(props: {
  tiles: ServiceTile[];
  showVolume: boolean;
  windowPhrase: string;
  isSubscribed: (tile: ServiceTile) => boolean;
  isViaApplication: (tile: ServiceTile) => boolean;
  onToggleMail: (tile: ServiceTile) => void;
  onOpen: (tile: ServiceTile) => void;
}) {
  const heading = "px-1.5 text-left font-normal whitespace-nowrap";

  return (
    <div
      data-testid="service-rows"
      className="overflow-x-auto rounded-[11px] border border-[#1E344C] bg-card"
    >
      <table className="w-full border-collapse">
        <thead>
          <tr className="h-8 border-b border-[#17293D] bg-[#0B1826] font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">
            <th className={`${heading} pl-4`}>SERVICE</th>
            <th className={heading}>STATUS</th>
            <th className={`${heading} text-right`}>ERRORS</th>
            <th className={`${heading} text-right`}>WARN</th>
            <th className={`${heading} text-right`}>LOGS</th>
            {props.showVolume && <th className={heading}>VOLUME</th>}
            <th className={`${heading} w-full`}>LATEST EPISODE</th>
            <th className={`${heading} pr-4`}>MAIL</th>
          </tr>
        </thead>
        <tbody>
          {props.tiles.map(tile => {
            const open = tile.episodes !== undefined && tile.episodes.open > 0;
            const latest = tile.episodes?.latest;
            const logs =
              tile.volume === undefined
                ? undefined
                : tile.volume.error + tile.volume.warn + tile.volume.info + tile.volume.debug;
            return (
              <tr
                key={tile.serviceId}
                data-testid="service-row"
                className={`h-10 cursor-pointer border-b border-[#101F31] hover:bg-[#12243A] ${
                  open ? "bg-[rgba(229,84,74,0.07)]" : ""
                }`}
                onClick={() => props.onOpen(tile)}
              >
                <td className="py-0 pr-1.5 pl-4 align-middle font-mono text-xs whitespace-nowrap">
                  {tile.label}
                </td>
                <td className="px-1.5 align-middle">
                  <div className="flex items-center gap-2 whitespace-nowrap">
                    <span
                      className={`h-[15px] w-[3px] shrink-0 rounded-[2px] ${
                        open && latest != null
                          ? severityRailClass(Number(latest.firstLogSeverity))
                          : "bg-[#1E344C]"
                      }`}
                    />
                    <StatusBadges tile={tile} />
                  </div>
                </td>
                <Cell value={tile.volume?.error} tone="error" />
                <Cell value={tile.volume?.warn} tone="warn" />
                <Cell value={logs} tone="neutral" />
                {props.showVolume && (
                  <td className="px-1.5 align-middle">
                    <div className="w-[90px]">
                      <Sparkline buckets={tile.volume?.buckets} mini />
                    </div>
                  </td>
                )}
                <td className="w-full max-w-0 px-1.5 align-middle">
                  <div className="truncate font-mono text-[11.5px] text-[#DCE8F3]">
                    {tile.episodes === undefined ? (
                      <span className="text-[#4A6480]">—</span>
                    ) : open && latest != null ? (
                      <>
                        <LiveDuration since={latest.openedAt} />
                        {latest.firstLogBody != null && ` · ${latest.firstLogBody}`}
                      </>
                    ) : latest?.closedAt != null ? (
                      `all clear ${describeMillis(Date.now() - Date.parse(latest.closedAt))} ago`
                    ) : (
                      <span className="text-[#8CA1B8]">no episode in {props.windowPhrase}</span>
                    )}
                  </div>
                </td>
                <td className="py-0 pr-4 pl-1.5 text-right align-middle whitespace-nowrap">
                  <MailToggle
                    subscribed={props.isSubscribed(tile)}
                    viaApplication={props.isViaApplication(tile)}
                    applicationName={tile.applicationName}
                    onToggle={() => props.onToggleMail(tile)}
                  />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function Cell(props: { value: number | undefined; tone: "error" | "warn" | "neutral" }) {
  const color =
    props.value === undefined || props.value === 0
      ? "text-[#4A6480]"
      : props.tone === "error"
        ? "text-severity-error"
        : props.tone === "warn"
          ? "text-severity-warn"
          : "text-[#B6C8DA]";
  return (
    <td className={`px-1.5 text-right align-middle font-mono text-[11.5px] whitespace-nowrap ${color}`}>
      {props.value === undefined ? "—" : props.value.toLocaleString()}
    </td>
  );
}
