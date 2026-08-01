import type { ServiceTile } from "./serviceOverview";

/** One chip vocabulary for the whole board — group headings, cards and table rows agree. */
export const BADGE =
  "rounded-[5px] border px-2 py-0.5 font-mono text-[10.5px] uppercase tracking-[0.08em] whitespace-nowrap";

export const BADGE_TROUBLE = "border-[rgba(229,84,74,0.3)] bg-[rgba(229,84,74,0.14)] text-[#F0685A]";
export const BADGE_QUIETED = "border-[rgba(233,164,60,0.32)] bg-[rgba(233,164,60,0.12)] text-[#E9A43C]";
export const BADGE_CLEAR = "border-state-solved/25 bg-state-solved/10 text-state-solved";

/**
 * The card's and the row's status: a 7 px dot, then the badge. Open pulses; a service that is
 * both open and quieted shows the open badge and appends the quieted count. The calm green is
 * `--state-solved`, never brass — brass means interactive, not "good news".
 */
export function StatusBadges(props: { tile: ServiceTile }) {
  const episodes = props.tile.episodes;
  if (episodes === undefined) {
    return (
      <>
        <span className="size-[7px] shrink-0 rounded-full bg-[#4A6480]" />
        <span className="font-mono text-[10.5px] text-[#4A6480]">—</span>
      </>
    );
  }

  if (episodes.open > 0) {
    return (
      <>
        <span className="size-[7px] shrink-0 rounded-full bg-[#E5544A] animate-[bpulse_1.6s_ease-in-out_infinite]" />
        <span className={`${BADGE} ${BADGE_TROUBLE}`}>
          {episodes.open === 1 ? "open episode" : `${episodes.open} open episodes`}
        </span>
        {episodes.quieted > 0 && (
          <span className={`${BADGE} ${BADGE_QUIETED}`}>{episodes.quieted} quieted</span>
        )}
      </>
    );
  }

  if (episodes.quieted > 0) {
    return (
      <>
        <span className="size-[7px] shrink-0 rounded-full bg-[#C97B12]" />
        <span className={`${BADGE} ${BADGE_QUIETED}`}>
          {episodes.quieted === 1 ? "quieted episode" : `${episodes.quieted} quieted episodes`}
        </span>
      </>
    );
  }

  return (
    <>
      <span className="bg-state-solved size-[7px] shrink-0 rounded-full" />
      <span className={`${BADGE} ${BADGE_CLEAR}`}>all clear</span>
    </>
  );
}

/** The group heading's worst-state chip: any open → in trouble, else quieted, else all clear. */
export function GroupBadge(props: { inTrouble: number; quieted: number }) {
  if (props.inTrouble > 0) {
    return <span className={`${BADGE} ${BADGE_TROUBLE}`}>{props.inTrouble} in trouble</span>;
  }
  if (props.quieted > 0) {
    return <span className={`${BADGE} ${BADGE_QUIETED}`}>{props.quieted} quieted</span>;
  }
  return <span className={`${BADGE} ${BADGE_CLEAR}`}>all clear</span>;
}
