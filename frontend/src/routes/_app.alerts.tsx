import { createFileRoute, useNavigate } from "@tanstack/react-router";

import { EpisodesPage } from "@/features/alerting/EpisodesPage";
import { SubscriptionsPanel } from "@/features/alerting/SubscriptionsPanel";

type AlertsSection = "episodes" | "subscriptions";

export const Route = createFileRoute("/_app/alerts")({
  validateSearch: (search: Record<string, unknown>): { section: AlertsSection } => ({
    section: search.section === "subscriptions" ? "subscriptions" : "episodes",
  }),
  component: AlertsRoute,
});

function AlertsRoute() {
  const { section } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-end gap-6 border-b border-[#17293D] bg-[#0B1826] px-6 pt-5">
        <div className="flex flex-col gap-1 pb-3.5">
          <h1 className="text-[19px] font-semibold tracking-[-0.4px]">Alerts</h1>
          <p className="text-[12.5px] text-[#8CA1B8]">
            Episodes of trouble, and which services you want to hear about.
          </p>
        </div>
        <nav className="ml-auto flex gap-0.5">
          <Tab
            label="Episodes"
            active={section === "episodes"}
            onClick={() => navigate({ search: { section: "episodes" }, replace: true })}
          />
          <Tab
            label="Subscriptions"
            active={section === "subscriptions"}
            onClick={() => navigate({ search: { section: "subscriptions" }, replace: true })}
          />
        </nav>
      </div>

      <div className="min-h-0 flex-1">
        {section === "episodes" ? <EpisodesPage /> : <SubscriptionsPanel />}
      </div>
    </div>
  );
}

function Tab(props: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={props.onClick}
      className={`rounded-t-lg px-[15px] py-[9px] text-[13px] ${
        props.active
          ? "bg-background text-[#F6C170] shadow-[inset_0_2px_0_#E9A43C]"
          : "text-[#8CA1B8] hover:text-foreground"
      }`}
    >
      {props.label}
    </button>
  );
}
