import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api, type FingerprintRule } from "@/api/client";
import { Checkbox } from "@/components/ui/checkbox";
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

import { useApplicationAlerting } from "./ApplicationAlertingCard";
import { GroupingHelpDialog } from "./GroupingHelpDialog";
import { RegroupConfirmation } from "./RegroupConfirmation";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

/** How many open episodes the warning counts before it stops counting and says "at least". */
const COUNT_LIMIT = 500;

interface Scope {
  byNamespace: boolean;
  byEnvironment: boolean;
  byServiceName: boolean;
}

interface Proposal {
  rule: FingerprintRule | null;
  attributeKey: string | null;
  scope: Scope | null;
}

/**
 * What "the same trouble" means for this Application: the Fingerprint Rule that distills its kinds
 * of trouble (ADR 0033) and the Episode Scope that says how far one Episode reaches (ADR 0034).
 * Application-wide with no service tier under either, because an Episode reaches across Services
 * and the two ends must agree on what they mean.
 *
 * Changing either leaves every open Logs Episode in a partition nothing will report again, so
 * saving Mutes them and drops the quiet windows keyed on the old Fingerprints. That is why this
 * card, alone among the settings, asks before it saves — and says afterwards what it cost.
 */
export function ApplicationGroupingCard(props: { applicationId: string }) {
  const t = useT();
  const queryClient = useQueryClient();
  const alerting = useApplicationAlerting(props.applicationId);
  const [pending, setPending] = useState<Proposal | null>(null);
  const [regrouped, setRegrouped] = useState<{ muted: number; dropped: number } | null>(null);

  // What the change will cost, counted from the episodes themselves: only the Logs Watch's are
  // re-partitioned — a Health Check Episode's kind is reserved and its Scope is its Service's.
  const openLogsEpisodes = useQuery({
    queryKey: ["admin", "grouping-cost", props.applicationId],
    enabled: pending !== null,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: { applicationId: props.applicationId, state: ["Open"], limit: COUNT_LIMIT },
        },
      });
      if (error !== undefined) throw new Error(t.registry.groupingCard.countFailed);
      return {
        count: data.items.filter(episode => episode.watch === "Logs").length,
        capped: data.items.length === COUNT_LIMIT,
      };
    },
  });

  const save = useMutation({
    mutationFn: async (body: Proposal) => {
      const { data, error } = await api.PUT(
        "/api/admin/applications/{applicationId}/alerting/grouping",
        { params: { path: { applicationId: props.applicationId } }, body },
      );
      if (error !== undefined || data === undefined) {
        throw new Error(t.registry.groupingCard.saveFailed);
      }
      return data;
    },
    onSuccess: async answer => {
      setPending(null);
      setRegrouped({
        muted: Number(answer.mutedEpisodes),
        dropped: Number(answer.droppedQuietWindows),
      });
      await queryClient.invalidateQueries({ queryKey: ["admin", "alerting", props.applicationId] });
      await queryClient.invalidateQueries({ queryKey: ["alerts"] });
    },
  });

  const data = alerting.data;
  if (data === undefined) return null;

  const grouping = data.grouping;
  const scope: Scope = grouping.scope ?? data.defaults.scope;
  // Absence is what the default is stored as, so the two read as one option and picking it back
  // stores nothing — "absent = inherit" stays the single truth, as it does for every other setting.
  const defaultRule = data.defaults.fingerprintRule ?? "ThrowingCode";

  const propose = (next: Partial<Proposal>) => {
    setRegrouped(null);
    setPending({
      rule: next.rule !== undefined ? next.rule : (grouping.rule ?? null),
      attributeKey: next.attributeKey !== undefined
        ? next.attributeKey
        : (grouping.attributeKey ?? null),
      scope: next.scope ?? scope,
    });
  };

  return (
    <div className="flex flex-col gap-3 rounded-[11px] border border-[#1E344C] bg-card p-4">
      {/* The dropdown below cannot say what a rung means, and the reader is a developer. */}
      <span className="flex items-center gap-2">
        <span className={CAPTION}>{t.registry.groupingCard.caption}</span>
        <GroupingHelpDialog />
      </span>

      <div className="flex flex-wrap items-end gap-3">
        <div className="grid gap-1.5">
          <Label htmlFor={`grouping-rule-${props.applicationId}`}>
            {t.registry.groupingCard.ruleLabel}
          </Label>
          <Select
            value={grouping.rule ?? defaultRule}
            disabled={save.isPending}
            onValueChange={next =>
              propose({ rule: next === defaultRule ? null : (next as FingerprintRule) })}
          >
            <SelectTrigger id={`grouping-rule-${props.applicationId}`} className="w-[232px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ThrowingCode">
                {t.registry.groupingCard.rule.ThrowingCode}
              </SelectItem>
              <SelectItem value="KindOfFailure">
                {t.registry.groupingCard.rule.KindOfFailure}
              </SelectItem>
              <SelectItem value="WhatWasSaid">
                {t.registry.groupingCard.rule.WhatWasSaid}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor={`grouping-attribute-${props.applicationId}`}>
            {t.registry.groupingCard.attributeLabel}
          </Label>
          <Input
            id={`grouping-attribute-${props.applicationId}`}
            key={grouping.attributeKey ?? ""}
            className="w-[232px]"
            placeholder={t.registry.groupingCard.attributePlaceholder}
            defaultValue={grouping.attributeKey ?? ""}
            disabled={save.isPending}
            onBlur={event => {
              const next = event.currentTarget.value.trim();
              if (next !== (grouping.attributeKey ?? "")) {
                propose({ attributeKey: next === "" ? null : next });
              }
            }}
          />
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <span className={CAPTION}>{t.registry.groupingCard.scopeCaption}</span>
        <div className="flex flex-wrap items-center gap-5">
          <ScopeFacet
            id={`scope-ns-${props.applicationId}`}
            label={t.registry.groupingCard.byNamespace}
            checked={scope.byNamespace}
            disabled={save.isPending}
            onChange={byNamespace => propose({ scope: { ...scope, byNamespace } })}
          />
          <ScopeFacet
            id={`scope-env-${props.applicationId}`}
            label={t.registry.groupingCard.byEnvironment}
            checked={scope.byEnvironment}
            disabled={save.isPending}
            onChange={byEnvironment => propose({ scope: { ...scope, byEnvironment } })}
          />
          <ScopeFacet
            id={`scope-name-${props.applicationId}`}
            label={t.registry.groupingCard.byServiceName}
            checked={scope.byServiceName}
            disabled={save.isPending}
            onChange={byServiceName => propose({ scope: { ...scope, byServiceName } })}
          />
        </div>
      </div>

      {/* The change is irreversible and lands on episodes somebody may be working, so it asks
          in a modal rather than in a line the eye can skip past. */}
      <RegroupConfirmation
        open={pending !== null}
        onOpenChange={open => {
          if (!open) {
            setPending(null);
            save.reset();
          }
        }}
        cost={openLogsEpisodes.data}
        pending={save.isPending}
        failed={save.error?.message}
        onConfirm={() => pending !== null && save.mutate(pending)}
      />

      {regrouped !== null && (
        <p data-testid="regroup-done" className="text-[12.5px] text-[#8CA1B8]">
          {t.registry.groupingCard.done(regrouped.muted, regrouped.dropped)}
        </p>
      )}

      <p className="text-[11.5px] text-[#7D93AA]">{t.registry.groupingCard.explainer}</p>
    </div>
  );
}

function ScopeFacet(props: {
  id: string;
  label: string;
  checked: boolean;
  disabled: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <div className="flex items-center gap-2">
      <Checkbox
        id={props.id}
        checked={props.checked}
        disabled={props.disabled}
        onCheckedChange={checked => props.onChange(checked === true)}
      />
      <Label htmlFor={props.id} className="cursor-pointer font-normal">
        {props.label}
      </Label>
    </div>
  );
}
