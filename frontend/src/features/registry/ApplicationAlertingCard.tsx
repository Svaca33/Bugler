import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CircleHelpIcon } from "lucide-react";
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
import { useT } from "@/i18n";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

/** The value the Select uses for "no setting — the defaults apply". */
const INHERIT = "__inherit__";

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
  const t = useT();
  const queryClient = useQueryClient();
  const alerting = useApplicationAlerting(props.applicationId);
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "alerting", props.applicationId] });

  const save = useMutation({
    mutationFn: async (body: {
      sensitivity: Sensitivity | null;
      quietWindowMinutes: number | null;
      claimLeaseHours: number | null;
    }) => {
      const { error } = await api.PUT("/api/admin/applications/{applicationId}/alerting", {
        params: { path: { applicationId: props.applicationId } },
        body,
      });
      if (error !== undefined) throw new Error(t.registry.alertingCard.saveFailed);
    },
    onSuccess: invalidate,
  });

  const data = alerting.data;
  if (data === undefined) {
    return null;
  }

  // The generated client types integers as `number | string`; the UI works in numbers.
  const quietWindow = data.quietWindowMinutes == null ? null : Number(data.quietWindowMinutes);
  const claimLease = data.claimLeaseHours == null ? null : Number(data.claimLeaseHours);

  const defaultSensitivityLabel =
    t.registry.alertingCard.sensitivity[data.defaults.sensitivity ?? "Errors"];

  return (
    <div className="flex flex-col gap-3 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <span className={CAPTION}>
        {t.registry.alertingCard.caption(defaultSensitivityLabel, data.defaults.quietWindowMinutes)}
      </span>
      <div className="flex flex-wrap items-end gap-3">
        <SensitivityField
          id={`alerting-sensitivity-${props.applicationId}`}
          value={data.sensitivity}
          inheritLabel={t.registry.alertingCard.defaultOption(defaultSensitivityLabel)}
          disabled={save.isPending}
          onChange={sensitivity =>
            save.mutate({ sensitivity, quietWindowMinutes: quietWindow, claimLeaseHours: claimLease })}
        />
        <QuietWindowField
          id={`alerting-quiet-${props.applicationId}`}
          value={quietWindow}
          placeholder={`${data.defaults.quietWindowMinutes}`}
          disabled={save.isPending}
          help={t.registry.alertingCard.quietWindowHelp}
          onCommit={quietWindowMinutes =>
            save.mutate({
              sensitivity: data.sensitivity ?? null,
              quietWindowMinutes,
              claimLeaseHours: claimLease,
            })}
        />
        <ClaimLeaseField
          id={`alerting-lease-${props.applicationId}`}
          value={claimLease}
          placeholder={`${data.defaults.claimLeaseHours}`}
          disabled={save.isPending}
          help={t.registry.alertingCard.claimLeaseHelp}
          onCommit={claimLeaseHours =>
            save.mutate({
              sensitivity: data.sensitivity ?? null,
              quietWindowMinutes: quietWindow,
              claimLeaseHours,
            })}
        />
        <WebhookField applicationId={props.applicationId} webhook={data.chatWebhook} />
      </div>
      {save.error !== null && (
        <p className="text-[12.5px] text-[#F0685A]">{save.error.message}</p>
      )}
      <p className="text-[11.5px] text-[#7D93AA]">{t.registry.alertingCard.explainer}</p>
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
  const t = useT();
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={props.id}>{t.registry.alertingCard.sensitivityLabel}</Label>
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
          <SelectItem value="Off">{t.registry.alertingCard.sensitivity.Off}</SelectItem>
          <SelectItem value="Errors">{t.registry.alertingCard.sensitivity.Errors}</SelectItem>
          <SelectItem value="ErrorsAndWarnings">
            {t.registry.alertingCard.sensitivity.ErrorsAndWarnings}
          </SelectItem>
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
  help?: string;
  onCommit: (value: number | null) => void;
}) {
  const t = useT();
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
      <div className="flex items-center gap-1.5">
        <Label htmlFor={props.id}>{t.registry.alertingCard.quietWindowLabel}</Label>
        {props.help !== undefined && <HelpTip text={props.help} />}
      </div>
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

/**
 * How long a Machine Claim's lease runs for this application's episodes (Alerting CONTEXT.md:
 * Machine Claim) — the same commit-on-blur shape as the quiet window beside it.
 */
function ClaimLeaseField(props: {
  id: string;
  value: number | null | undefined;
  placeholder: string;
  disabled: boolean;
  help: string;
  onCommit: (value: number | null) => void;
}) {
  const t = useT();
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
      <div className="flex items-center gap-1.5">
        <Label htmlFor={props.id}>{t.registry.alertingCard.claimLeaseLabel}</Label>
        <HelpTip text={props.help} />
      </div>
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

/** A ?-in-a-circle whose tooltip explains the field beside it; shows on hover and keyboard focus. */
function HelpTip(props: { text: string }) {
  return (
    <span className="group relative inline-flex" tabIndex={0} aria-label={props.text}>
      <CircleHelpIcon className="size-3.5 cursor-help text-[#5F7590] group-hover:text-[#8CA1B8]" />
      <span
        role="tooltip"
        className="pointer-events-none absolute top-full left-1/2 z-20 mt-1.5 hidden w-[264px] -translate-x-1/2 rounded-[7px] border border-[#1E344C] bg-[#0B1826] p-2.5 text-[11.5px] leading-relaxed font-normal text-[#A9BDD1] shadow-[0_8px_24px_rgba(0,0,0,0.45)] group-hover:block group-focus-visible:block"
      >
        {props.text}
      </span>
    </span>
  );
}

function WebhookField(props: {
  applicationId: string;
  webhook: ApplicationAlerting["chatWebhook"];
}) {
  const t = useT();
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
      if (error !== undefined) throw new Error(t.registry.alertingCard.webhookInvalid);
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
        <Label>{t.registry.alertingCard.webhookLabel}</Label>
        <div className="flex h-9 items-center gap-2">
          <span className="rounded-[5px] bg-[#16283C] px-[7px] py-0.5 font-mono text-[10.5px] text-[#A9BDD1]">
            {t.registry.alertingCard.webhookSet(props.webhook.domain)}
          </span>
          <Button size="sm" variant="ghost" onClick={() => setEditing(true)}>
            {t.registry.alertingCard.replaceButton}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className="text-[#F0685A]"
            disabled={saveWebhook.isPending}
            onClick={() => saveWebhook.mutate(null)}
          >
            {t.registry.alertingCard.removeButton}
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={`alerting-webhook-${props.applicationId}`}>
        {t.registry.alertingCard.webhookLabel}
      </Label>
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
          {t.registry.alertingCard.saveButton}
        </Button>
        {props.webhook != null && (
          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
            {t.registry.cancel}
          </Button>
        )}
      </div>
      {saveWebhook.error !== null && (
        <p className="text-[12.5px] text-[#F0685A]">{saveWebhook.error.message}</p>
      )}
    </div>
  );
}
