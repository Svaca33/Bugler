import { useT } from "@/i18n";

import type { ServiceTile } from "./serviceOverview";

/** One chip vocabulary for the whole board — group headings, cards and table rows agree. */
export const BADGE =
  "rounded-[5px] border px-2 py-0.5 font-mono text-[10.5px] uppercase tracking-[0.08em] whitespace-nowrap";

export const BADGE_TROUBLE = "border-[rgba(229,84,74,0.3)] bg-[rgba(229,84,74,0.14)] text-[#F0685A]";
export const BADGE_QUIETED = "border-[rgba(233,164,60,0.32)] bg-[rgba(233,164,60,0.12)] text-[#E9A43C]";
export const BADGE_CLEAR = "border-state-solved/25 bg-state-solved/10 text-state-solved";

/**
 * The card's and the row's status badge. A service that is both open and quieted shows the open
 * badge and appends the quieted count. The calm green is `--state-solved`, never brass — brass
 * means interactive, not "good news". The badge tones carry the state alone: no dots.
 */
export function StatusBadges(props: { tile: ServiceTile }) {
  const t = useT();
  const episodes = props.tile.episodes;
  if (episodes === undefined) {
    return <span className="font-mono text-[10.5px] text-[#4A6480]">—</span>;
  }

  if (episodes.open > 0) {
    return (
      <>
        <span className={`${BADGE} ${BADGE_TROUBLE}`}>
          {t.overview.badge.openEpisodes(episodes.open)}
        </span>
        {episodes.quieted > 0 && (
          <span className={`${BADGE} ${BADGE_QUIETED}`}>
            {t.overview.badge.quieted(episodes.quieted)}
          </span>
        )}
      </>
    );
  }

  if (episodes.quieted > 0) {
    return (
      <span className={`${BADGE} ${BADGE_QUIETED}`}>
        {t.overview.badge.quietedEpisodes(episodes.quieted)}
      </span>
    );
  }

  return <span className={`${BADGE} ${BADGE_CLEAR}`}>{t.overview.badge.allClear}</span>;
}

/** The group heading's worst-state chip: any open → in trouble, else quieted, else all clear. */
export function GroupBadge(props: { inTrouble: number; quieted: number }) {
  const t = useT();
  if (props.inTrouble > 0) {
    return (
      <span className={`${BADGE} ${BADGE_TROUBLE}`}>{t.overview.badge.inTrouble(props.inTrouble)}</span>
    );
  }
  if (props.quieted > 0) {
    return (
      <span className={`${BADGE} ${BADGE_QUIETED}`}>{t.overview.badge.quieted(props.quieted)}</span>
    );
  }
  return <span className={`${BADGE} ${BADGE_CLEAR}`}>{t.overview.badge.allClear}</span>;
}
