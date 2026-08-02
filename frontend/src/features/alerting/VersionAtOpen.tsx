import { describeMillis } from "@/lib/duration";
import type { VersionAtInstant } from "@/lib/releases";

/**
 * The Declared Version a Service was running when an Episode opened, and — when it had just been
 * released — how long before.
 *
 * The version shows whenever it is known, without a threshold: it is a fact about the Episode, and
 * a list where every Episode since Tuesday reads `1.2.4` and none reads `1.2.3` says more than any
 * single row could. The elapsed time is the part that needs one, because "6 days after 1.2.3"
 * insinuates a connection nobody would draw.
 *
 * Renders nothing at all for a Service that declares no version — the intended degradation
 * (ADR 0016), and the reason this is never a placeholder or a dash.
 */
export function VersionAtOpen(props: { at: VersionAtInstant | undefined; className?: string }) {
  const { at } = props;
  if (at === undefined) return null;

  const released = at.releasedMsBefore !== undefined;
  return (
    <span
      className={`${released ? "text-primary" : ""} ${props.className ?? ""}`.trim()}
      title={released
        ? `Released ${describeMillis(at.releasedMsBefore!)} before this episode opened`
        : "The version this service was running when the episode opened"}
    >
      on {at.version}
      {released && ` · released ${describeMillis(at.releasedMsBefore!)} before`}
    </span>
  );
}
