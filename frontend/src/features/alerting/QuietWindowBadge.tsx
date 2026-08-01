import type { Episode } from "@/api/client";

import { quietWindowWords } from "./quietWindow";

/**
 * Marks an Episode whose kind of trouble keeps a Quiet Window of its own (ADR 0004) — nothing at
 * all while it inherits. Shown wherever Episodes are listed, so a tuned kind is never a surprise
 * discovered only in the detail panel.
 */
export function QuietWindowBadge(props: { episode: Episode }) {
  const own = props.episode.fingerprintQuietWindowMinutes;
  if (own == null) {
    return null;
  }

  return (
    <span
      className="flex-none rounded-sm border border-[#2C4159] px-[5px] text-[#A9BDD1]"
      title="This kind of trouble keeps its own quiet window instead of the service's"
    >
      quiet window {quietWindowWords(Number(own))}
    </span>
  );
}
