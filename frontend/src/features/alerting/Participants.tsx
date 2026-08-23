import { useT } from "@/i18n";

import type { Episode } from "@/api/client";
import type { KnownService } from "./serviceIndex";

/** How many Services a row names before it starts counting the rest instead. */
const SHOWN = 3;

/**
 * Which Services and versions are in an Episode (see CONTEXT.md: Participation) — the answer to
 * "is it still happening on the version we just shipped, and is it every deployment or only one".
 *
 * Since ADR 0034 an Episode has no single Service, so this stands where the Service's name used
 * to. It is also why the Release overlay no longer names the version here: the Episode states its
 * own, from each Match's `service.version`, rather than the browser inferring one from Releases.
 */
export function Participants(props: {
  episode: Episode;
  services: Map<string, KnownService>;
  muted?: boolean;
}) {
  const t = useT();
  const { participations } = props.episode;
  if (participations.length === 0) return null;

  const shown = participations.slice(0, SHOWN);
  const rest = participations.length - shown.length;

  return (
    <>
      {shown.map((participation, index) => (
        <span
          key={`${participation.serviceId}\u0000${participation.version ?? ""}`}
          className={props.muted === true ? undefined : "text-[#A9BDD1]"}
        >
          {props.services.get(participation.serviceId)?.facets.name ?? "—"}
          {participation.version !== null && (
            <span className="text-[#7D93AA]">
              {" "}
              {t.alerting.version.on(participation.version)}
            </span>
          )}
          {index < shown.length - 1 && <span className="text-[#7D93AA]">,</span>}
        </span>
      ))}
      {rest > 0 && (
        <span title={t.alerting.participants.moreTitle(rest)}>
          {t.alerting.participants.more(rest)}
        </span>
      )}
    </>
  );
}
