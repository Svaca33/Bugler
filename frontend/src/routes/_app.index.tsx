import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";

import { FocusEmptyState, useIsWatchingNothing } from "@/features/access/FocusEmptyState";
import { asFilters } from "@/features/explore/attributeFilters";
import { LogsPage, type LogFilters } from "@/features/explore/LogsPage";
import { asInstant, asRange, DEFAULT_RANGE } from "@/features/explore/timeFilter";

/** The page's URL state: the Filter plus, apart from it, which Log Record is open in the panel. */
export interface LogsSearch extends LogFilters {
  log?: number;
}

export const Route = createFileRoute("/_app/")({
  validateSearch: (search: Record<string, unknown>): LogsSearch => ({
    applicationId: asString(search.applicationId),
    namespace: asString(search.namespace),
    environment: asString(search.environment),
    service: asString(search.service),
    severityMin: asNumber(search.severityMin),
    range: asRange(search.range),
    from: asInstant(search.from),
    to: asInstant(search.to),
    q: asString(search.q),
    traceId: asString(search.traceId),
    filters: asFilters(search.filters),
    log: asNumber(search.log),
  }),
  // Arriving without a Time Filter starts on the default window; clearing it later is a `stay`,
  // so "All time" — the absence of all three parameters — is still reachable.
  beforeLoad: ({ search, cause }) => {
    if (cause === "enter" && !hasTimeFilter(search)) {
      throw redirect({ to: "/", search: { ...search, range: DEFAULT_RANGE }, replace: true });
    }
  },
  component: LogsRoute,
});

function LogsRoute() {
  // `log` stays outside the LogFilters object: it selects a record, it narrows nothing, and it
  // must not invalidate the list, count, or volume queries keyed on the Filter.
  const { log, ...filters } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });

  // The route decides, not the page: Access owns the Focus and Exploration owns the log list, and
  // a route is where two contexts are allowed to meet (as on the Admin page).
  if (useIsWatchingNothing() === true) {
    return <FocusEmptyState />;
  }

  return (
    <LogsPage
      filters={filters}
      onChange={next => navigate({ search: { ...next, log }, replace: true })}
      selectedLogId={log}
      onSelectLog={next => navigate({ search: previous => ({ ...previous, log: next }), replace: true })}
    />
  );
}

function hasTimeFilter(search: LogsSearch): boolean {
  return search.range !== undefined || search.from !== undefined || search.to !== undefined;
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function asNumber(value: unknown): number | undefined {
  const parsed = Number(value);
  return value !== undefined && Number.isFinite(parsed) ? parsed : undefined;
}
