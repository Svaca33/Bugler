import { createFileRoute } from "@tanstack/react-router";

import { TraceDetailPage } from "@/features/explore/TraceDetailPage";

export const Route = createFileRoute("/_app/traces/$traceId")({
  component: TraceDetailRoute,
});

function TraceDetailRoute() {
  const { traceId } = Route.useParams();
  return <TraceDetailPage traceId={traceId} />;
}
