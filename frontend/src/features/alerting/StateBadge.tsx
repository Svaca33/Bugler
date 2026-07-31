import type { Episode } from "@/api/client";

const STYLES: Record<Episode["state"], string> = {
  Open: "text-severity-error",
  // Quieted wears the warn amber: the episode closed on the timer, nobody judged it.
  Quieted: "text-severity-warn",
  Solved: "text-state-solved",
  Muted: "text-[#6E86A0]",
};

/** The Episode's place in its lifecycle, in the list's own compact voice. */
export function StateBadge({ state }: { state: Episode["state"] }) {
  return (
    <span
      className={`flex items-center gap-1.5 font-mono text-[10.5px] uppercase tracking-[0.08em] ${STYLES[state]}`}
    >
      {state === "Open" && (
        <span className="size-1.5 animate-[bpulse_1.6s_ease-in-out_infinite] rounded-full bg-severity-error-rail" />
      )}
      {state.toLowerCase()}
    </span>
  );
}
