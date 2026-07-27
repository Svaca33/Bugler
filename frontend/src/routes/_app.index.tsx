import { createFileRoute, useNavigate } from "@tanstack/react-router";

import { LogsPage, type LogFilters } from "@/features/explore/LogsPage";

export const Route = createFileRoute("/_app/")({
  validateSearch: (search: Record<string, unknown>): LogFilters => ({
    applicationId: asString(search.applicationId),
    instanceId: asString(search.instanceId),
    severityMin: asNumber(search.severityMin),
    q: asString(search.q),
    tenant: asString(search.tenant),
    traceId: asString(search.traceId),
  }),
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

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function asNumber(value: unknown): number | undefined {
  const parsed = Number(value);
  return value !== undefined && Number.isFinite(parsed) ? parsed : undefined;
}
