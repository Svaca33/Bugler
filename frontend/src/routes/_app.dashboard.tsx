import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";

import {
  isDefaultBoard,
  readDashboardSearch,
  sanitizeDashboardSearch,
  writeDashboardSearch,
} from "@/features/overview/dashboardMemory";
import { DashboardPage } from "@/features/overview/DashboardPage";

export const Route = createFileRoute("/_app/dashboard")({
  // Absent parameters are the defaults (P1D, cards, volume shown, app·namespace·environment),
  // so the clean `/dashboard` stays clean; `range` takes any ISO-8601 duration, same as Logs.
  validateSearch: sanitizeDashboardSearch,
  // Arriving at a bare /dashboard restores the reader's remembered board. Parameters in the URL
  // always win, and setting a control back to its default later is a `stay` — the defaults
  // remain reachable, they just are not re-imposed on the next visit.
  beforeLoad: ({ search, cause }) => {
    if (cause !== "enter" || !isDefaultBoard(search)) return;
    const saved = readDashboardSearch();
    if (saved !== undefined) {
      throw redirect({ to: "/dashboard", search: saved, replace: true });
    }
  },
  component: DashboardRoute,
});

function DashboardRoute() {
  const search = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  return (
    <DashboardPage
      search={search}
      onChange={next => {
        // Only a touched control writes the memory — opening someone's deep link never does.
        writeDashboardSearch(next);
        navigate({ search: next, replace: true });
      }}
    />
  );
}
