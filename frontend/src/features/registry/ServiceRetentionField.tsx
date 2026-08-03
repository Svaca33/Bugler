import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useT, type Messages } from "@/i18n";

import { effectiveRetentionDays, parseRetentionInput, shortensRetention } from "./retentionChange";

/**
 * What one of the two clocks is called wherever the field has to name it — the catalog's own
 * shape, so the active language supplies the label, the confirmation's name and its subject.
 */
export type RetentionClock = Messages["registry"]["retention"]["logs"];

/**
 * One Service's Retention Policy — both clocks — wired to the API. The endpoint takes the policy
 * whole rather than a patch, so each field sends its own change together with the other as it
 * stands; a null there would clear an override nobody touched.
 */
export function ServiceRetentionField(props: {
  applicationId: string;
  serviceId: string;
  retentionDays: number | null;
  traceRetentionDays: number | null;
  defaultRetentionDays: number;
  defaultTraceRetentionDays: number;
}) {
  const t = useT();
  const logs = useRetentionSave(props.applicationId, props.serviceId);
  const traces = useRetentionSave(props.applicationId, props.serviceId);

  return (
    <>
      <RetentionField
        id={`${props.serviceId}-logs`}
        clock={t.registry.retention.logs}
        value={props.retentionDays}
        defaultRetentionDays={props.defaultRetentionDays}
        pending={logs.isPending}
        error={logs.error}
        onSave={days =>
          logs.mutateAsync({ retentionDays: days, traceRetentionDays: props.traceRetentionDays })
        }
      />
      <RetentionField
        id={`${props.serviceId}-traces`}
        clock={t.registry.retention.traces}
        value={props.traceRetentionDays}
        defaultRetentionDays={props.defaultTraceRetentionDays}
        pending={traces.isPending}
        error={traces.error}
        onSave={days =>
          traces.mutateAsync({ retentionDays: props.retentionDays, traceRetentionDays: days })
        }
      />
    </>
  );
}

/** One field's own saving, so a failure on one clock is not reported under the other. */
function useRetentionSave(applicationId: string, serviceId: string) {
  const t = useT();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (policy: {
      retentionDays: number | null;
      traceRetentionDays: number | null;
    }) => {
      const { error } = await api.PUT("/api/admin/services/{id}/retention", {
        params: { path: { id: serviceId } },
        body: policy,
      });
      if (error !== undefined) throw new Error(t.registry.retention.saveFailed);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "services", applicationId] }),
  });
}

/**
 * How long one of this Service's two clocks keeps its telemetry. Empty means the server default,
 * like every other override on this card. Lengthening saves straight away; shortening hands stored
 * telemetry to the next purge, so it asks first — including when the shortening comes from clearing
 * an override that stood above the default.
 */
export function RetentionField(props: {
  id: string;
  clock: RetentionClock;
  value: number | null;
  defaultRetentionDays: number;
  pending: boolean;
  error: Error | null;
  onSave: (days: number | null) => Promise<unknown>;
}) {
  const [confirming, setConfirming] = useState<{ days: number | null } | null>(null);
  // Bumped to remount the input, which is the only way to take back what was typed but never
  // saved — the stored value alone cannot tell an abandoned edit from no edit at all.
  const [revision, setRevision] = useState(0);

  const commit = (raw: string) => {
    const parsed = parseRetentionInput(raw);
    if (!parsed.valid) {
      return; // The API refuses it anyway; an uncommitted field is clearer than an error.
    }

    if (parsed.days === props.value) {
      return;
    }

    if (shortensRetention(props.value, parsed.days, props.defaultRetentionDays)) {
      setConfirming({ days: parsed.days });
      return;
    }

    void props.onSave(parsed.days).catch(() => {}); // Reported through `error`.
  };

  const abandon = () => {
    setConfirming(null);
    setRevision(current => current + 1);
  };

  const confirm = async () => {
    if (confirming === null) {
      return;
    }

    try {
      await props.onSave(confirming.days);
      setConfirming(null);
    } catch {
      // Stays open, naming the failure — a shortening that did not happen must not look done.
    }
  };

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={`retention-${props.id}`}>{props.clock.label}</Label>
      <Input
        id={`retention-${props.id}`}
        key={`${props.value ?? ""}-${revision}`}
        className="w-[136px]"
        type="number"
        min={1}
        placeholder={`${props.defaultRetentionDays}`}
        defaultValue={props.value ?? ""}
        disabled={props.pending}
        onBlur={event => commit(event.currentTarget.value)}
        onKeyDown={event => {
          if (event.key === "Enter") {
            event.preventDefault();
            commit(event.currentTarget.value);
          }
        }}
      />
      {props.error !== null && confirming === null && (
        <p className="text-[12.5px] text-[#F0685A]">{props.error.message}</p>
      )}

      {confirming !== null && (
        <ShorteningConfirmation
          clock={props.clock}
          proposed={confirming.days}
          defaultRetentionDays={props.defaultRetentionDays}
          pending={props.pending}
          failed={props.error !== null}
          onCancel={abandon}
          onConfirm={confirm}
        />
      )}
    </div>
  );
}

/**
 * The guard on a shortening. Unlike a Deletion it does not ask for a phrase to be typed: the
 * policy can be raised again the moment it was wrong, and only the telemetry already swept up
 * is gone. It names the cutoff but refuses to promise a time, because when the purge next runs
 * is a deployment setting.
 */
function ShorteningConfirmation(props: {
  clock: RetentionClock;
  proposed: number | null;
  defaultRetentionDays: number;
  pending: boolean;
  failed: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useT();
  const days = effectiveRetentionDays(props.proposed, props.defaultRetentionDays);

  return (
    <Dialog open onOpenChange={open => !open && props.onCancel()}>
      <DialogContent>
        <div className="grid gap-4">
          <DialogHeader>
            <DialogTitle>{t.registry.retention.shortenTitle(props.clock.name, days)}</DialogTitle>
            <DialogDescription>
              {props.proposed === null && t.registry.retention.followsDefault(days)}
              {t.registry.retention.purgeConsequence(props.clock.subject, days)}
            </DialogDescription>
          </DialogHeader>

          {props.failed && (
            <p className="text-[11.5px] text-destructive">
              {t.registry.retention.saveFailedUnchanged}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={props.onCancel}>
              {t.registry.cancel}
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={props.pending}
              onClick={props.onConfirm}
            >
              {t.registry.retention.shortenButton}
            </Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
