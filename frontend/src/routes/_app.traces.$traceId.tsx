import { createFileRoute } from "@tanstack/react-router";

import { asFilters } from "@/features/explore/attributeFilters";
import { TraceDetailPage } from "@/features/explore/TraceDetailPage";
import type { TraceFilters } from "@/features/explore/TracesPage";

export const Route = createFileRoute("/_app/traces/$traceId")({
  // Carries the traces-list filters through the detail so span magnifiers merge into them on return.
  validateSearch: (search: Record<string, unknown>): TraceFilters => ({
    applicationId: asString(search.applicationId),
    namespace: asString(search.namespace),
    environment: asString(search.environment),
    service: asString(search.service),
    errorsOnly: search.errorsOnly === true || search.errorsOnly === "true" ? true : undefined,
    filters: asFilters(search.filters),
  }),
  component: TraceDetailRoute,
});

function TraceDetailRoute() {
  const { traceId } = Route.useParams();
  const listFilters = Route.useSearch();
  return <TraceDetailPage traceId={traceId} listFilters={listFilters} />;
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}
