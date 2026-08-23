import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";

import { FocusEmptyState, useIsWatchingNothing } from "@/features/access/FocusEmptyState";
import { asFilters } from "@/features/explore/attributeFilters";
import { asInstant, asRange, DEFAULT_RANGE } from "@/features/explore/timeFilter";
import { TracesPage, type TraceFilters } from "@/features/explore/TracesPage";

export const Route = createFileRoute("/_app/traces/")({
  validateSearch: (search: Record<string, unknown>): TraceFilters => ({
    applicationId: asString(search.applicationId),
    namespace: asString(search.namespace),
    environment: asString(search.environment),
    service: asString(search.service),
    range: asRange(search.range),
    from: asInstant(search.from),
    to: asInstant(search.to),
    errorsOnly: search.errorsOnly === true || search.errorsOnly === "true" ? true : undefined,
    filters: asFilters(search.filters),
  }),
  // Same as Logs: the default window is spelled out in the URL, and clearing it stays cleared.
  beforeLoad: ({ search, cause }) => {
    if (cause === "enter" && !hasTimeFilter(search)) {
      throw redirect({ to: "/traces", search: { ...search, range: DEFAULT_RANGE }, replace: true });
    }
  },
  component: TracesRoute,
});

function TracesRoute() {
  const filters = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });

  if (useIsWatchingNothing() === true) {
    return <FocusEmptyState />;
  }

  return (
    <TracesPage filters={filters} onChange={next => navigate({ search: next, replace: true })} />
  );
}

function hasTimeFilter(search: TraceFilters): boolean {
  return search.range !== undefined || search.from !== undefined || search.to !== undefined;
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}
