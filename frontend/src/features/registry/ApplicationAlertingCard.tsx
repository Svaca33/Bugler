import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api, type ApplicationAlerting, type Sensitivity } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

/** The value the Select uses for "no setting — the defaults apply". */
const INHERIT = "__inherit__";

export const SENSITIVITY_LABELS: Record<Sensitivity, string> = {
  Off: "Off",
  Errors: "Errors",
  ErrorsAndWarnings: "Errors + warnings",
};

export function useApplicationAlerting(applicationId: string) {
  return useQuery({
    queryKey: ["admin", "alerting", applicationId],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/applications/{applicationId}/alerting", {
        params: { path: { applicationId } },
      });
      if (error !== undefined) throw new Error("Failed to load alerting settings");
      return data;
    },
  });
}

/**
 * The Application's detection settings, sibling to retention: Sensitivity and quiet window apply
 * to every service without an override, and the Chat Webhook is write-only — after saving, only
 * its host ever comes back.
 */
export function ApplicationAlertingCard(props: { applicationId: string }) {
  const queryClient = useQueryClient();
  const alerting = useApplicationAlerting(props.applicationId);
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "alerting", props.applicationId] });

  const save = useMutation({
    mutationFn: async (body: { sensitivity: Sensitivity | null; quietWindowMinutes: number | null }) => {
      const { error } = await api.PUT("/api/admin/applications/{applicationId}/alerting", {
        params: { path: { applicationId: props.applicationId } },
        body,
      });
      if (error !== undefined) throw new Error("Failed to save alerting settings");
    },
    onSuccess: invalidate,
  });

  const data = alerting.data;
  if (data === undefined) {
    return null;
  }

  // The generated client types integers as `number | string`; the UI works in numbers.
  const quietWindow = data.quietWindowMinutes == null ? null : Number(data.quietWindowMinutes);

  return (
    <div className="flex flex-col gap-3 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <span className={CAPTION}>
        ALERTING · defaults {SENSITIVITY_LABELS[data.defaults.sensitivity ?? "Errors"]} ·{" "}
        {data.defaults.quietWindowMinutes} min quiet window
      </span>
      <div className="flex flex-wrap items-end gap-3">
        <SensitivityField
          id={`alerting-sensitivity-${props.applicationId}`}
          value={data.sensitivity}
          inheritLabel={`Default (${SENSITIVITY_LABELS[data.defaults.sensitivity ?? "Errors"]})`}
          disabled={save.isPending}
          onChange={sensitivity =>
            save.mutate({ sensitivity, quietWindowMinutes: quietWindow })}
        />
        <QuietWindowField
          id={`alerting-quiet-${props.applicationId}`}
          value={quietWindow}
          placeholder={`${data.defaults.quietWindowMinutes}`}
          disabled={save.isPending}
          onCommit={quietWindowMinutes =>
            save.mutate({ sensitivity: data.sensitivity ?? null, quietWindowMinutes })}
        />
        <WebhookField applicationId={props.applicationId} webhook={data.chatWebhook} />
      </div>
      {save.error !== null && (
        <p className="text-[12.5px] text-[#F0685A]">{save.error.message}</p>
      )}
      <p className="text-[11.5px] text-[#7D93AA]">
        Off closes open episodes immediately and silently. Who gets mailed is each person's own
        choice under Alerts → Subscriptions; the webhook posts every episode of this application
        to one Google Chat space.
      </p>
    </div>
  );
}

export function SensitivityField(props: {
  id: string;
  value: Sensitivity | null | undefined;
  inheritLabel: string;
  disabled: boolean;
  onChange: (value: Sensitivity | null) => void;
}) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={props.id}>Sensitivity</Label>
      <Select
        value={props.value ?? INHERIT}
        onValueChange={next => props.onChange(next === INHERIT ? null : (next as Sensitivity))}
        disabled={props.disabled}
      >
        <SelectTrigger id={props.id} className="w-[196px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={INHERIT}>{props.inheritLabel}</SelectItem>
          <SelectItem value="Off">{SENSITIVITY_LABELS.Off}</SelectItem>
          <SelectItem value="Errors">{SENSITIVITY_LABELS.Errors}</SelectItem>
          <SelectItem value="ErrorsAndWarnings">{SENSITIVITY_LABELS.ErrorsAndWarnings}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  );
}

export function QuietWindowField(props: {
  id: string;
  value: number | null | undefined;
  placeholder: string;
  disabled: boolean;
  onCommit: (value: number | null) => void;
}) {
  const commit = (raw: string) => {
    const trimmed = raw.trim();
    const next = trimmed === "" ? null : Number(trimmed);
    if (next !== null && (!Number.isInteger(next) || next < 1)) {
      return; // The API refuses it anyway; an uncommitted field is clearer than an error.
    }

    if (next !== (props.value ?? null)) {
      props.onCommit(next);
    }
  };

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={props.id}>Quiet window (min)</Label>
      <Input
        id={props.id}
        key={`${props.value ?? ""}`}
        className="w-[136px]"
        type="number"
        min={1}
        placeholder={props.placeholder}
        defaultValue={props.value ?? ""}
        disabled={props.disabled}
        onBlur={event => commit(event.currentTarget.value)}
        onKeyDown={event => {
          if (event.key === "Enter") {
            event.preventDefault();
            commit(event.currentTarget.value);
          }
        }}
      />
    </div>
  );
}

function WebhookField(props: {
  applicationId: string;
  webhook: ApplicationAlerting["chatWebhook"];
}) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [url, setUrl] = useState("");

  const saveWebhook = useMutation({
    mutationFn: async (next: string | null) => {
      const { error } = await api.PUT(
        "/api/admin/applications/{applicationId}/alerting/webhook",
        {
          params: { path: { applicationId: props.applicationId } },
          body: { url: next },
        },
      );
      if (error !== undefined) throw new Error("The webhook must be an absolute https URL.");
    },
    onSuccess: () => {
      setEditing(false);
      setUrl("");
      queryClient.invalidateQueries({ queryKey: ["admin", "alerting", props.applicationId] });
    },
  });

  if (props.webhook != null && !editing) {
    return (
      <div className="grid gap-1.5">
        <Label>Google Chat webhook</Label>
        <div className="flex h-9 items-center gap-2">
          <span className="rounded-[5px] bg-[#16283C] px-[7px] py-0.5 font-mono text-[10.5px] text-[#A9BDD1]">
            set · {props.webhook.domain}
          </span>
          <Button size="sm" variant="ghost" onClick={() => setEditing(true)}>
            Replace
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className="text-[#F0685A]"
            disabled={saveWebhook.isPending}
            onClick={() => saveWebhook.mutate(null)}
          >
            Remove
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={`alerting-webhook-${props.applicationId}`}>Google Chat webhook</Label>
      <div className="flex items-center gap-2">
        <Input
          id={`alerting-webhook-${props.applicationId}`}
          className="w-[280px]"
          placeholder="https://chat.googleapis.com/…"
          value={url}
          onChange={event => setUrl(event.target.value)}
        />
        <Button
          size="sm"
          disabled={url.trim() === "" || saveWebhook.isPending}
          onClick={() => saveWebhook.mutate(url.trim())}
        >
          Save
        </Button>
        {props.webhook != null && (
          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
            Cancel
          </Button>
        )}
      </div>
      {saveWebhook.error !== null && (
        <p className="text-[12.5px] text-[#F0685A]">{saveWebhook.error.message}</p>
      )}
    </div>
  );
}
