import { createFileRoute, useNavigate } from "@tanstack/react-router";

import { TracesPage, type TraceFilters } from "@/features/explore/TracesPage";

export const Route = createFileRoute("/_app/traces/")({
  validateSearch: (search: Record<string, unknown>): TraceFilters => ({
    applicationId: typeof search.applicationId === "string" ? search.applicationId : undefined,
    instanceId: typeof search.instanceId === "string" ? search.instanceId : undefined,
    errorsOnly: search.errorsOnly === true || search.errorsOnly === "true" ? true : undefined,
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
