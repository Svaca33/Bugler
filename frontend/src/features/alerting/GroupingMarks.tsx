import { useT } from "@/i18n";

import type { Episode } from "@/api/client";

/**
 * What Bugler admits about how it grouped this Episode (ADR 0033): a rung it had to coarsen to, a
 * stack it could not read whole, and Alerts a Storm folded away. Each is a thing to be seen rather
 * than guessed at — a parser written wrong shows up here, never as a plausible answer over nonsense.
 *
 * Nothing renders on an Episode where all three are the good case, which is most of them.
 */
export function GroupingMarks(props: { episode: Episode; className?: string }) {
  const { episode } = props;
  const t = useT();
  // Version 0 is a legacy row: its rung says how it was made, not that anything degraded.
  const coarsened = Number(episode.recipeVersion) > 0
    && (episode.fingerprintRung === "Failure" || episode.fingerprintRung === "Message");

  if (!coarsened && !episode.stackTruncated && !episode.alertFoldedIntoStorm) return null;

  return (
    <span className={`flex items-center gap-[6px] ${props.className ?? ""}`.trim()}>
      {coarsened && <Mark label={t.alerting.grouping.coarsened} title={t.alerting.grouping.coarsenedTitle} />}
      {episode.stackTruncated && (
        <Mark label={t.alerting.grouping.truncated} title={t.alerting.grouping.truncatedTitle} />
      )}
      {episode.alertFoldedIntoStorm && (
        <Mark label={t.alerting.grouping.storm} title={t.alerting.grouping.stormTitle} />
      )}
    </span>
  );
}

function Mark(props: { label: string; title: string }) {
  return (
    <span
      className="rounded-sm border border-[#2C4159] px-[5px] text-[#8CA1B8]"
      title={props.title}
    >
      {props.label}
    </span>
  );
}
