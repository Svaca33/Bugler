import { createFileRoute, useNavigate } from "@tanstack/react-router";

import { asFilters } from "@/features/explore/attributeFilters";
import { TracesPage, type TraceFilters } from "@/features/explore/TracesPage";

export const Route = createFileRoute("/_app/traces/")({
  validateSearch: (search: Record<string, unknown>): TraceFilters => ({
    applicationId: asString(search.applicationId),
    namespace: asString(search.namespace),
    environment: asString(search.environment),
    service: asString(search.service),
    errorsOnly: search.errorsOnly === true || search.errorsOnly === "true" ? true : undefined,
    filters: asFilters(search.filters),
  }),
  component: TracesRoute,
});

function TracesRoute() {
  const filters = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  return (
    <TracesPage filters={filters} onChange={next => navigate({ search: next, replace: true })} />
  );
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}
