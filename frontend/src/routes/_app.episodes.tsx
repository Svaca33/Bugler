import { createFileRoute, useNavigate } from "@tanstack/react-router";

import {
  asAck,
  asLifecycle,
  asOpened,
  type EpisodesFilters,
} from "@/features/alerting/episodesFilter";
import { EpisodesPage } from "@/features/alerting/EpisodesPage";
import { SubscriptionsPanel } from "@/features/alerting/SubscriptionsPanel";

type EpisodesSection = "episodes" | "subscriptions";

/** The page's URL state: the section, the rail's filters, and which Episode is open in the panel. */
export interface EpisodesSearch extends EpisodesFilters {
  section: EpisodesSection;
  episode?: string;
}

export const Route = createFileRoute("/_app/episodes")({
  validateSearch: (search: Record<string, unknown>): EpisodesSearch => ({
    section: search.section === "subscriptions" ? "subscriptions" : "episodes",
    lifecycle: asLifecycle(search.lifecycle),
    ack: asAck(search.ack),
    applicationId: asString(search.applicationId),
    namespace: asString(search.namespace),
    environment: asString(search.environment),
    service: asString(search.service),
    opened: asOpened(search.opened),
    q: asString(search.q),
    episode: asString(search.episode),
  }),
  component: EpisodesRoute,
});

function EpisodesRoute() {
  // `episode` stays outside the filters object: it selects an Episode, it narrows nothing,
  // and it must not invalidate the list, band or counts queries keyed on the filters.
  const { section, episode, ...filters } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-end gap-6 border-b border-[#17293D] bg-[#0B1826] px-6 pt-4">
        <div className="flex flex-col gap-[3px] pb-[13px]">
          <h1 className="text-[19px] font-semibold tracking-[-0.4px]">Episodes</h1>
          <p className="text-[12.5px] text-[#8CA1B8]">
            Episodes of trouble in the services you can see, and which of them mail you.
          </p>
        </div>
        <nav className="ml-auto flex gap-0.5">
          <Tab
            label="Episodes"
            active={section === "episodes"}
            onClick={() =>
              navigate({ search: previous => ({ ...previous, section: "episodes" }), replace: true })}
          />
          <Tab
            label="Subscriptions"
            active={section === "subscriptions"}
            onClick={() =>
              navigate({
                search: previous => ({ ...previous, section: "subscriptions" }),
                replace: true,
              })}
          />
        </nav>
      </div>

      <div className="min-h-0 flex-1">
        {section === "episodes" ? (
          <EpisodesPage
            filters={filters}
            onChange={next =>
              navigate({ search: { section, episode, ...next }, replace: true })}
            selectedId={episode}
            onSelect={id =>
              navigate({ search: previous => ({ ...previous, episode: id }), replace: true })}
          />
        ) : (
          <SubscriptionsPanel />
        )}
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

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}
