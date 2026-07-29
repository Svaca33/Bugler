import { useMutation, useQueryClient } from "@tanstack/react-query";

import { api, type Sensitivity } from "@/api/client";

import {
  QuietWindowField,
  SENSITIVITY_LABELS,
  SensitivityField,
  useApplicationAlerting,
} from "./ApplicationAlertingCard";

/**
 * One service's overrides of its application's alerting: empty means inherit, and clearing both
 * fields removes the override entirely — the retention "empty = server default" semantic.
 */
export function ServiceAlertingOverride(props: { applicationId: string; serviceId: string }) {
  const queryClient = useQueryClient();
  const alerting = useApplicationAlerting(props.applicationId);

  const save = useMutation({
    mutationFn: async (body: { sensitivity: Sensitivity | null; quietWindowMinutes: number | null }) => {
      const { error } = await api.PUT("/api/admin/services/{serviceId}/alerting", {
        params: { path: { serviceId: props.serviceId } },
        body,
      });
      if (error !== undefined) throw new Error("Failed to save the alerting override");
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
  const applicationSensitivity =
    SENSITIVITY_LABELS[data.sensitivity ?? data.defaults.sensitivity ?? "Errors"];
  const applicationQuietWindow = data.quietWindowMinutes ?? data.defaults.quietWindowMinutes;

  return (
    <div className="flex flex-wrap items-end gap-3">
      <SensitivityField
        id={`override-sensitivity-${props.serviceId}`}
        value={override?.sensitivity}
        inheritLabel={`Inherit (${applicationSensitivity})`}
        disabled={save.isPending}
        onChange={sensitivity =>
          save.mutate({ sensitivity, quietWindowMinutes: overrideQuietWindow })}
      />
      <QuietWindowField
        id={`override-quiet-${props.serviceId}`}
        value={overrideQuietWindow}
        placeholder={`${applicationQuietWindow}`}
        disabled={save.isPending}
        onCommit={quietWindowMinutes =>
          save.mutate({ sensitivity: override?.sensitivity ?? null, quietWindowMinutes })}
      />
      {save.error !== null && (
        <p className="text-[12.5px] text-[#F0685A]">{save.error.message}</p>
      )}
    </div>
  );
}
