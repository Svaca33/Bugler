import { useState } from "react";

import { useCatalog } from "@/api/queries";
import type { EpisodeCounts } from "@/api/client";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { FilterGroup, FilterRail } from "@/components/ui/filter-rail";
import { FilterSelect } from "@/components/ui/filter-select";
import { useT } from "@/i18n";
import { facetOptions } from "@/lib/sourceFilter";

import {
  DEFAULT_OPENED,
  EPISODE_STATES,
  OPENED_ALL,
  OPENED_PRESETS,
  effectiveLifecycle,
  normalizeLifecycle,
  type EpisodesFilters,
  type EpisodeStateName,
} from "./episodesFilter";

/**
 * The left rail of the Episodes tab. Everything it writes round-trips through the URL, so a
 * triage view is linkable; Clear all returns to the defaults, not to emptiness.
 */
export function EpisodesFilterRail(props: {
  filters: EpisodesFilters;
  counts: EpisodeCounts | undefined;
  onChange: (filters: EpisodesFilters) => void;
}) {
  const { filters, counts, onChange } = props;
  const t = useT();
  const catalog = useCatalog();
  const applications = catalog.data?.applications ?? [];
  const [search, setSearch] = useState(filters.q ?? "");
  const lifecycle = effectiveLifecycle(filters);

  const toggleState = (state: EpisodeStateName) => {
    const next = lifecycle.includes(state)
      ? lifecycle.filter(s => s !== state)
      : [...lifecycle, state];
    onChange({ ...filters, lifecycle: normalizeLifecycle(next) });
  };

  const countOf: Record<EpisodeStateName, number | undefined> = counts === undefined
    ? { Open: undefined, Quieted: undefined, Solved: undefined, Muted: undefined }
    : {
        Open: Number(counts.open),
        Quieted: Number(counts.quieted),
        Solved: Number(counts.solved),
        Muted: Number(counts.muted),
      };

  const canClear = [
    filters.lifecycle, filters.ack, filters.archived, filters.applicationId, filters.namespace,
    filters.environment, filters.service, filters.opened, filters.q,
  ].some(value => value !== undefined);

  return (
    <FilterRail
      canClear={canClear}
      onClear={() => {
        setSearch("");
        onChange({});
      }}
    >
      <FilterGroup label={t.alerting.filters.lifecycle}>
        {EPISODE_STATES.map(state => (
          <div key={state} className="flex items-center gap-[9px]">
            <Checkbox
              id={`lifecycle-${state}`}
              checked={lifecycle.includes(state)}
              onCheckedChange={() => toggleState(state)}
            />
            <Label htmlFor={`lifecycle-${state}`} className="font-normal text-[12.5px]">
              {t.alerting.state.filterLabel[state]}
            </Label>
            <span
              className={`ml-auto font-mono text-[11px] ${
                state === "Open" ? "text-[#F0685A]" : "text-[#7D93AA]"
              }`}
            >
              {countOf[state] ?? "—"}
            </span>
          </div>
        ))}
      </FilterGroup>

      {/*
        A filter of its own, never a fifth lifecycle box: Archived is a mark laid on top of a
        state (Alerting CONTEXT.md), so it says nothing about how the trouble ended. The count
        stands whether the rows are shown or not — hidden must never read as absent.
      */}
      <FilterGroup label={t.alerting.filters.archived}>
        <div className="flex items-center gap-[9px]">
          <Checkbox
            id="archived"
            checked={filters.archived === true}
            onCheckedChange={checked =>
              onChange({ ...filters, archived: checked === true ? true : undefined })}
          />
          <Label htmlFor="archived" className="font-normal text-[12.5px]">
            {t.alerting.filters.showArchived}
          </Label>
          <span className="ml-auto font-mono text-[11px] text-[#7D93AA]">
            {counts === undefined ? "—" : Number(counts.archived)}
          </span>
        </div>
      </FilterGroup>

      <FilterGroup label={t.alerting.filters.whoIsOnIt}>
        {([
          { value: "none", label: t.alerting.filters.nobodyYet },
          { value: "me", label: t.alerting.filters.acknowledgedByMe },
        ] as const).map(option => (
          <div key={option.value} className="flex items-center gap-[9px]">
            <Checkbox
              id={`ack-${option.value}`}
              checked={filters.ack === option.value}
              // Mutually exclusive by construction: checking one is setting the value.
              onCheckedChange={checked =>
                onChange({ ...filters, ack: checked === true ? option.value : undefined })}
            />
            <Label htmlFor={`ack-${option.value}`} className="font-normal text-[12.5px]">
              {option.label}
            </Label>
          </div>
        ))}
      </FilterGroup>

      <FilterGroup label={t.alerting.filters.source}>
        <FilterSelect
          className="w-full"
          placeholder={t.alerting.filters.allApplications}
          value={filters.applicationId}
          options={applications.map(a => ({ value: a.id, label: a.name }))}
          onChange={applicationId =>
            onChange({
              ...filters,
              applicationId,
              namespace: undefined,
              environment: undefined,
              service: undefined,
            })}
        />
        <FilterSelect
          className="w-full"
          placeholder={t.alerting.filters.allNamespaces}
          value={filters.namespace}
          options={facetOptions(applications, filters, "namespace").map(v => ({ value: v, label: v }))}
          onChange={namespace => onChange({ ...filters, namespace })}
        />
        <FilterSelect
          className="w-full"
          placeholder={t.alerting.filters.allEnvironments}
          value={filters.environment}
          options={facetOptions(applications, filters, "environment").map(v => ({ value: v, label: v }))}
          onChange={environment => onChange({ ...filters, environment })}
        />
        <FilterSelect
          className="w-full"
          placeholder={t.alerting.filters.allServices}
          value={filters.service}
          options={facetOptions(applications, filters, "service").map(v => ({ value: v, label: v }))}
          onChange={service => onChange({ ...filters, service })}
        />
      </FilterGroup>

      <FilterGroup label={t.alerting.filters.opened}>
        <FilterSelect
          className="w-full"
          placeholder={t.alerting.filters.anyTime}
          value={(filters.opened ?? DEFAULT_OPENED) === OPENED_ALL
            ? undefined
            : filters.opened ?? DEFAULT_OPENED}
          options={OPENED_PRESETS.map(value => ({
            value,
            label: t.alerting.filters.openedPreset[value] ?? value,
          }))}
          // The default window keeps a clean URL; "Any time" must be said out loud (see OPENED_ALL).
          onChange={value =>
            onChange({
              ...filters,
              opened: value === undefined ? OPENED_ALL : value === DEFAULT_OPENED ? undefined : value,
            })}
        />
      </FilterGroup>

      <FilterGroup label={t.alerting.filters.firstLogContains}>
        <form
          onSubmit={event => {
            event.preventDefault();
            onChange({ ...filters, q: search || undefined });
          }}
        >
          <Input
            placeholder={t.alerting.filters.searchPlaceholder}
            value={search}
            onChange={event => setSearch(event.target.value)}
          />
        </form>
      </FilterGroup>
    </FilterRail>
  );
}
