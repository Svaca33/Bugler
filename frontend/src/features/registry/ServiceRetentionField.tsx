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

import { effectiveRetentionDays, parseRetentionInput, shortensRetention } from "./retentionChange";

/** One Service's retention, wired to the API. */
export function ServiceRetentionField(props: {
  applicationId: string;
  serviceId: string;
  retentionDays: number | null;
  defaultRetentionDays: number;
}) {
  const queryClient = useQueryClient();

  const save = useMutation({
    mutationFn: async (days: number | null) => {
      const { error } = await api.PUT("/api/admin/services/{id}/retention", {
        params: { path: { id: props.serviceId } },
        body: { retentionDays: days },
      });
      if (error !== undefined) throw new Error("Failed to save the retention.");
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["admin", "services", props.applicationId] }),
  });

  return (
    <RetentionField
      id={props.serviceId}
      value={props.retentionDays}
      defaultRetentionDays={props.defaultRetentionDays}
      pending={save.isPending}
      error={save.error}
      onSave={days => save.mutateAsync(days)}
    />
  );
}

/**
 * How long this Service's telemetry is kept. Empty means the server default, like every other
 * override on this card. Lengthening saves straight away; shortening hands stored telemetry to
 * the next purge, so it asks first — including when the shortening comes from clearing an
 * override that stood above the default.
 */
export function RetentionField(props: {
  id: string;
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
      <Label htmlFor={`retention-${props.id}`}>Retention (days)</Label>
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
  proposed: number | null;
  defaultRetentionDays: number;
  pending: boolean;
  failed: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const days = effectiveRetentionDays(props.proposed, props.defaultRetentionDays);

  return (
    <Dialog open onOpenChange={open => !open && props.onCancel()}>
      <DialogContent>
        <div className="grid gap-4">
          <DialogHeader>
            <DialogTitle>Shorten retention to {days} days?</DialogTitle>
            <DialogDescription>
              {props.proposed === null &&
                `This service will follow the server default of ${days} days. `}
              Logs and spans older than {days} days will be permanently deleted at the next purge
              run. This cannot be undone.
            </DialogDescription>
          </DialogHeader>

          {props.failed && (
            <p className="text-[11.5px] text-destructive">
              Saving failed — the retention is unchanged.
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={props.onCancel}>
              Cancel
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={props.pending}
              onClick={props.onConfirm}
            >
              Shorten retention
            </Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
