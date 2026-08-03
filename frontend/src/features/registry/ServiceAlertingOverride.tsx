import { useMutation, useQueryClient } from "@tanstack/react-query";

import { api, type Sensitivity } from "@/api/client";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useT } from "@/i18n";

import {
  QuietWindowField,
  SensitivityField,
  useApplicationAlerting,
} from "./ApplicationAlertingCard";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

interface AlertingBody {
  sensitivity: Sensitivity | null;
  quietWindowMinutes: number | null;
  healthCheckUrl: string | null;
}

/**
 * One service's alerting, grouped by the watch each setting belongs to. The grouping is the
 * point, not decoration: sensitivity is the logs watch's switch and says nothing about the health
 * check, so "Off" must visibly sit under Logs rather than look like a service-wide mute.
 *
 * Empty means inherit, and clearing every field removes the override row entirely — the retention
 * "empty = server default" semantic. The health check address is the exception that inherits
 * nothing: every service answers at its own address.
 */
export function ServiceAlertingOverride(props: { applicationId: string; serviceId: string }) {
  const t = useT();
  const queryClient = useQueryClient();
  const alerting = useApplicationAlerting(props.applicationId);

  const save = useMutation({
    mutationFn: async (body: AlertingBody) => {
      const { data, error } = await api.PUT("/api/admin/services/{serviceId}/alerting", {
        params: { path: { serviceId: props.serviceId } },
        body,
      });
      if (error !== undefined) throw new Error(t.registry.alertingCard.overrideSaveFailed);
      return data;
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["admin", "alerting", props.applicationId] }),
  });

  const data = alerting.data;
  if (data === undefined) {
    return null;
  }

  const override = data.serviceOverrides.find(o => o.serviceId === props.serviceId);
  // The generated client types integers as `number | string`; the UI works in numbers.
  const overrideQuietWindow =
    override?.quietWindowMinutes == null ? null : Number(override.quietWindowMinutes);
  const healthCheckUrl = override?.healthCheckUrl ?? null;
  const applicationSensitivity =
    t.registry.alertingCard.sensitivity[data.sensitivity ?? data.defaults.sensitivity ?? "Errors"];
  const applicationQuietWindow = data.quietWindowMinutes ?? data.defaults.quietWindowMinutes;

  const current: AlertingBody = {
    sensitivity: override?.sensitivity ?? null,
    quietWindowMinutes: overrideQuietWindow,
    healthCheckUrl,
  };

  return (
    <div className="flex flex-col gap-3">
      <WatchGroup label={t.registry.alertingCard.logsWatch}>
        <SensitivityField
          id={`override-sensitivity-${props.serviceId}`}
          value={override?.sensitivity}
          inheritLabel={t.registry.alertingCard.inheritOption(applicationSensitivity)}
          disabled={save.isPending}
          onChange={sensitivity => save.mutate({ ...current, sensitivity })}
        />
        <QuietWindowField
          id={`override-quiet-${props.serviceId}`}
          value={overrideQuietWindow}
          placeholder={`${applicationQuietWindow}`}
          disabled={save.isPending}
          onCommit={quietWindowMinutes => save.mutate({ ...current, quietWindowMinutes })}
        />
      </WatchGroup>

      <WatchGroup label={t.registry.alertingCard.healthCheckWatch}>
        <HealthCheckField
          serviceId={props.serviceId}
          url={healthCheckUrl}
          pending={save.isPending}
          probe={save.data?.healthCheck ?? null}
          onSave={next => save.mutate({ ...current, healthCheckUrl: next })}
        />
      </WatchGroup>

      {save.error !== null && (
        <p className="text-[12.5px] text-[#F0685A]">{save.error.message}</p>
      )}
    </div>
  );
}

/** One watch's settings under its own name, so no field looks like it governs the others. */
function WatchGroup(props: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <span className={CAPTION}>{props.label}</span>
      <div className="flex flex-wrap items-end gap-3">{props.children}</div>
    </div>
  );
}

/**
 * Where Bugler asks this service whether it is alive. Shown in full rather than masked: it is an
 * address, not a credential, and the commonest mistake in it is a typo — which nobody can spot in
 * a field they cannot read. Commits on blur like every other field here, and clearing it is how
 * the watch is turned off; saving probes it once and says what came back.
 */
function HealthCheckField(props: {
  serviceId: string;
  url: string | null;
  pending: boolean;
  probe: { alive: boolean; detail: string } | null;
  onSave: (url: string | null) => void;
}) {
  const t = useT();
  const commit = (raw: string) => {
    const next = raw.trim() === "" ? null : raw.trim();
    if (next !== props.url) {
      props.onSave(next);
    }
  };

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={`override-health-${props.serviceId}`}>
        {t.registry.alertingCard.healthCheckUrlLabel}
      </Label>
      <Input
        id={`override-health-${props.serviceId}`}
        // Keyed on the saved value so a fresh one from the server replaces what is on screen.
        key={props.url ?? ""}
        className="w-[320px]"
        placeholder="http://backend:8080/health"
        defaultValue={props.url ?? ""}
        disabled={props.pending}
        onBlur={event => commit(event.currentTarget.value)}
        onKeyDown={event => {
          if (event.key === "Enter") {
            event.preventDefault();
            event.currentTarget.blur(); // Blur is what commits; Enter only asks for it.
          }
          if (event.key === "Escape") {
            event.currentTarget.value = props.url ?? "";
            event.currentTarget.blur();
          }
        }}
      />
      {props.probe !== null && (
        <p
          className={`font-mono text-[11px] ${
            props.probe.alive ? "text-[#5FBF8F]" : "text-[#F0685A]"
          }`}
        >
          {props.probe.alive
            ? t.registry.alertingCard.healthCheckAnswered
            : t.registry.alertingCard.healthCheckNoAnswer}{" "}
          · {props.probe.detail}
        </p>
      )}
      <p className="max-w-[400px] text-[11.5px] text-[#7D93AA]">
        {t.registry.alertingCard.healthCheckHelpBeforeCode} <code>localhost</code>{" "}
        {t.registry.alertingCard.healthCheckHelpAfterCode}
      </p>
    </div>
  );
}
