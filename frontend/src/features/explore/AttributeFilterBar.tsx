import { useQuery } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Combobox, type ComboboxOption } from "@/components/ui/combobox";
import { FilterChip } from "@/components/ui/filter-chip";
import { Input } from "@/components/ui/input";

import {
  displayPath,
  removeFilter,
  upsertFilter,
  type AttributeFilter,
  type FilterScope,
} from "./attributeFilters";
import type { SourceFilters } from "./sourceFilter";

interface ObservedKey {
  scope: FilterScope;
  path: string[];
}

/** Chips for the active Attribute Filters plus the add flow: pick an Observed Key, type a value. */
export function AttributeFilterBar(props: {
  signal: "logs" | "traces";
  /** Keeps the Observed Keys sample inside the same scope the list is showing. */
  source: SourceFilters;
  filters: AttributeFilter[];
  onChange: (filters: AttributeFilter[]) => void;
  /** `row` flows into a wrapping filter bar; `column` stacks full-width inside the filter rail. */
  layout?: "row" | "column";
}) {
  const { signal, source, filters, onChange, layout = "row" } = props;
  const column = layout === "column";
  const [picking, setPicking] = useState(false);
  const [pendingKey, setPendingKey] = useState<ObservedKey | null>(null);
  const [value, setValue] = useState("");

  const keys = useQuery({
    queryKey: [signal, "observed-keys", source],
    queryFn: async () => {
      const query = {
        applicationId: source.applicationId,
        namespace: source.namespace,
        environment: source.environment,
        service: source.service,
      };
      const { data, error } =
        signal === "logs"
          ? await api.GET("/api/logs/keys", { params: { query } })
          : await api.GET("/api/traces/keys", { params: { query } });
      if (error !== undefined) throw new Error("Failed to load observed keys");
      return data;
    },
    enabled: picking,
    staleTime: 30_000,
  });

  const items: ObservedKey[] = (keys.data?.items ?? []).map(item => ({
    scope: item.scope === "resource" ? "resource" : "attribute",
    path: item.path,
  }));
  const options: ComboboxOption[] = items.map((key, index) => ({
    value: String(index),
    label: displayPath(key.path),
    group: key.scope === "attribute" ? "Attributes" : "Resource",
  }));

  const cancel = () => {
    setPicking(false);
    setPendingKey(null);
    setValue("");
  };

  const add = () => {
    if (pendingKey === null) return;
    onChange(upsertFilter(filters, { ...pendingKey, value }));
    cancel();
  };

  // `contents` keeps the row layout exactly as it was: the wrapper is a box the browser skips,
  // so the chips and buttons stay direct children of the caller's flex-wrap bar.
  const full = column ? "w-full" : undefined;

  return (
    <div className={column ? "flex min-w-0 flex-col gap-1.5" : "contents"}>
      {filters.map(filter => (
        <FilterChip
          key={`${filter.scope}:${JSON.stringify(filter.path)}`}
          className={full}
          onRemove={() => onChange(removeFilter(filters, filter))}
        >
          {filter.scope === "resource" && <span className="text-muted-foreground">res:&thinsp;</span>}
          {displayPath(filter.path)} = {filter.value}
        </FilterChip>
      ))}
      {!picking && (
        <Button
          type="button"
          variant="outline"
          size="sm"
          className={full}
          onClick={() => setPicking(true)}
        >
          + Filter
        </Button>
      )}
      {picking && pendingKey === null && (
        <Combobox
          className={column ? "w-full" : "w-64"}
          autoFocus
          options={options}
          placeholder="Filter by key…"
          emptyText={keys.isPending ? "Loading keys…" : "No keys observed."}
          onSelect={selected => setPendingKey(items[Number(selected)] ?? null)}
          onBlur={cancel}
        />
      )}
      {picking && pendingKey !== null && (
        <span className={column ? "flex min-w-0 flex-col gap-1.5" : "flex items-center gap-1"}>
          <FilterChip className={full}>
            {pendingKey.scope === "resource" && <span className="text-muted-foreground">res:&thinsp;</span>}
            {displayPath(pendingKey.path)} =
          </FilterChip>
          <Input
            autoFocus
            className={column ? "h-8 w-full" : "h-8 w-44"}
            placeholder="Value"
            value={value}
            onChange={event => setValue(event.target.value)}
            onKeyDown={event => {
              if (event.key === "Enter") {
                event.preventDefault();
                add();
              } else if (event.key === "Escape") {
                cancel();
              }
            }}
          />
          <span className={column ? "flex gap-1.5" : "contents"}>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              className={column ? "flex-1" : undefined}
              onClick={add}
            >
              Add
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={cancel} aria-label="Cancel filter">
              ✕
            </Button>
          </span>
        </span>
      )}
    </div>
  );
}
