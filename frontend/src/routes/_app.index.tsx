import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";

import { asFilters } from "@/features/explore/attributeFilters";
import { LogsPage, type LogFilters } from "@/features/explore/LogsPage";
import { asInstant, asRange, DEFAULT_RANGE } from "@/features/explore/timeFilter";

export const Route = createFileRoute("/_app/")({
  validateSearch: (search: Record<string, unknown>): LogFilters => ({
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
  const filters = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  return (
    <LogsPage
      filters={filters}
      onChange={next => navigate({ search: next, replace: true })}
    />
  );
}

function hasTimeFilter(search: LogFilters): boolean {
  return search.range !== undefined || search.from !== undefined || search.to !== undefined;
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function asNumber(value: unknown): number | undefined {
  const parsed = Number(value);
  return value !== undefined && Number.isFinite(parsed) ? parsed : undefined;
}
