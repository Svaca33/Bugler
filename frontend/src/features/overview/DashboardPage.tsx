import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useMemo } from "react";

import { api } from "@/api/client";
import { useCatalog, useCurrentUser } from "@/api/queries";
import { useT } from "@/i18n";
import { describeDuration, durationMs } from "@/lib/duration";
import {
  isApplicationSubscribed,
  isServiceSubscribed,
  toggleService,
  type SubscriptionSet,
} from "@/lib/subscriptionSet";

import { GroupBadge } from "./badges";
import { DashboardControls } from "./DashboardControls";
import type { DashboardSearch } from "./dashboardMemory";
import { ServiceCard } from "./ServiceCard";
import {
  buildOverview,
  parseGroupBy,
  type CatalogEntry,
  type EpisodesByService,
  type Facet,
  type ServiceTile,
  type VolumeByService,
} from "./serviceOverview";
import { ServiceTable } from "./ServiceTable";

const DEFAULT_RANGE = "P1D";
const DEFAULT_GROUP_BY: Facet[] = ["app", "namespace", "environment"];

/** Both aggregates poll at 30 s, matching the Episodes page; nothing refetches on focus more often. */
const REFETCH_MS = 30_000;

/**
 * One tile per Service the caller may see, sorted so trouble is never below the fold: how loud
 * it is in the chosen window, whether an Episode is open on it right now, what shape its traffic
 * has, and whether the caller is mailed about it. A status board, not a triage tool — the moment
 * a reader wants to act on an Episode, the Episodes page owns that.
 */
export function DashboardPage(props: {
  search: DashboardSearch;
  onChange: (next: DashboardSearch) => void;
}) {
  const range = props.search.range ?? DEFAULT_RANGE;
  const layout = props.search.layout ?? "cards";
  const trouble = props.search.trouble === true;
  const showVolume = props.search.volume !== false;
  const groupBy =
    props.search.groupBy === undefined ? DEFAULT_GROUP_BY : parseGroupBy(props.search.groupBy);

  const t = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const user = useCurrentUser();
  const ready = user.data != null;

  const catalog = useCatalog({ refetchInterval: REFETCH_MS });

  // The table draws 18-bucket minis, the cards 24 — the key carries the ask so a layout switch
  // refetches once and then rides the same 30 s beat.
  const buckets = layout === "table" ? 18 : 24;
  const volume = useQuery({
    queryKey: ["overview", "volume", range, buckets],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/logs/volume/by-service", {
        params: { query: { range, buckets } },
      });
      if (error !== undefined) throw new Error("Failed to load volume");
      return data;
    },
    refetchInterval: REFETCH_MS,
    placeholderData: previous => previous,
    enabled: ready,
  });

  const episodes = useQuery({
    queryKey: ["overview", "episodes", range],
    queryFn: async () => {
      // Computed at fetch time, not render time, so the window rolls without re-keying.
      const from = new Date(Date.now() - (durationMs(range) ?? 86_400_000)).toISOString();
      const { data, error } = await api.GET("/api/alerting/episodes/by-service", {
        params: { query: { from } },
      });
      if (error !== undefined) throw new Error("Failed to load episodes");
      return data;
    },
    refetchInterval: REFETCH_MS,
    placeholderData: previous => previous,
    enabled: ready,
  });

  // The machine hand's standing marks: proposals awaiting a verdict, resignations calling for
  // a human hand. Counted by the alerting counts endpoint within the caller's visibility.
  const machineHand = useQuery({
    queryKey: ["alerts", "machine-hand-counts"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes/counts", {});
      if (error !== undefined) throw new Error("Failed to count the machine hand's marks");
      return data;
    },
    refetchInterval: REFETCH_MS,
    enabled: ready,
  });

  const subscriptions = useQuery({
    queryKey: ["alerts", "subscriptions"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/subscriptions");
      if (error !== undefined) throw new Error("Failed to load subscriptions");
      return data;
    },
    refetchInterval: REFETCH_MS,
    enabled: ready,
  });

  // A click sends the whole picture, optimistically; on failure, revert and say so.
  const update = useMutation({
    mutationFn: async (next: SubscriptionSet) => {
      const { error } = await api.PUT("/api/alerting/subscriptions", {
        body: { applicationIds: next.applicationIds, serviceIds: next.serviceIds },
      });
      if (error !== undefined) throw new Error("Failed to save subscriptions");
    },
    onMutate: async next => {
      await queryClient.cancelQueries({ queryKey: ["alerts", "subscriptions"] });
      const previous = queryClient.getQueryData(["alerts", "subscriptions"]);
      queryClient.setQueryData(["alerts", "subscriptions"], next);
      return { previous };
    },
    onError: (_error, _next, context) => {
      if (context?.previous !== undefined) {
        queryClient.setQueryData(["alerts", "subscriptions"], context.previous);
      }
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["alerts", "subscriptions"] }),
  });

  const entries = useMemo(
    (): CatalogEntry[] =>
      (catalog.data?.applications ?? []).flatMap(application =>
        application.services.map(service => ({
          serviceId: service.id,
          applicationId: application.id,
          applicationName: application.name,
          namespace: service.namespace,
          environment: service.environment,
          name: service.name,
        })),
      ),
    [catalog.data],
  );

  const volumeData = useMemo(
    (): VolumeByService | undefined =>
      volume.data === undefined
        ? undefined
        : {
            from: volume.data.from,
            to: volume.data.to,
            bucket: volume.data.bucket,
            services: volume.data.services.map(service => ({
              serviceId: service.serviceId,
              error: Number(service.error),
              warn: Number(service.warn),
              info: Number(service.info),
              debug: Number(service.debug),
              buckets: service.buckets.map(bucket => ({
                start: bucket.start,
                error: Number(bucket.error),
                warn: Number(bucket.warn),
                info: Number(bucket.info),
                debug: Number(bucket.debug),
              })),
            })),
          },
    [volume.data],
  );

  const episodesData = useMemo(
    (): EpisodesByService | undefined =>
      episodes.data === undefined
        ? undefined
        : {
            services: episodes.data.services.map(service => ({
              serviceId: service.serviceId,
              open: Number(service.open),
              quieted: Number(service.quieted),
              solved: Number(service.solved),
              muted: Number(service.muted),
              latest:
                service.latest === null
                  ? null
                  : {
                      id: service.latest.id,
                      state: service.latest.state,
                      openedAt: service.latest.openedAt,
                      closedAt: service.latest.closedAt,
                      lastMatchAt: service.latest.lastMatchAt,
                      errorCount: Number(service.latest.errorCount),
                      warnCount: Number(service.latest.warnCount),
                      watch: service.latest.watch,
                      firstMatchSeverity:
                        service.latest.firstMatchSeverity === null
                          ? null
                          : Number(service.latest.firstMatchSeverity),
                      firstMatchDetail: service.latest.firstMatchDetail,
                    },
            })),
          },
    [episodes.data],
  );

  const overview = useMemo(
    () =>
      buildOverview({
        catalog: entries,
        volume: volumeData,
        episodes: episodesData,
        // The table is one flat list by design — the sort does the work there.
        groupBy: layout === "table" ? [] : groupBy,
        troubleOnly: trouble,
      }),
    [entries, volumeData, episodesData, layout, groupBy, trouble],
  );

  const set: SubscriptionSet = {
    applicationIds: subscriptions.data?.applicationIds ?? [],
    serviceIds: subscriptions.data?.serviceIds ?? [],
  };

  const patch = (next: Partial<DashboardSearch>) => props.onChange({ ...props.search, ...next });

  const openLogs = (tile: ServiceTile) =>
    navigate({
      to: "/",
      search: {
        applicationId: tile.applicationId,
        namespace: tile.namespace,
        environment: tile.environment,
        service: tile.name,
        range,
      },
    });

  const openTraces = (tile: ServiceTile) =>
    navigate({
      to: "/traces",
      search: {
        applicationId: tile.applicationId,
        namespace: tile.namespace,
        environment: tile.environment,
        service: tile.name,
        range,
      },
    });

  // Only the SOURCE facets ride along: lifecycle and the opened window keep the Episodes
  // page's defaults, and the dashboard's window (which knows 1 h) does not map onto them anyway.
  const openEpisodes = (tile: ServiceTile) =>
    navigate({
      to: "/episodes",
      search: {
        section: "episodes",
        applicationId: tile.applicationId,
        namespace: tile.namespace,
        environment: tile.environment,
        service: tile.name,
      },
    });

  const toggleMail = (tile: ServiceTile) => {
    if (subscriptions.data === undefined) return;
    update.mutate(toggleService(set, tile.serviceId));
  };

  const missing = [
    ...(volume.isError ? [t.overview.errors.volumeAggregate] : []),
    ...(episodes.isError ? [t.overview.errors.episodesAggregate] : []),
  ];

  const windowDescribed = describeDuration(range);

  return (
    <div className="flex h-full flex-col">
      <div className="flex shrink-0 items-end gap-[18px] border-b border-[#17293D] bg-[#0B1826] px-6 pt-[18px] pb-3.5">
        <div>
          <h1 className="text-[19px] font-semibold tracking-[-0.4px]">{t.overview.title}</h1>
          <p className="text-[12.5px] text-[#8CA1B8]">{t.overview.subtitle}</p>
        </div>
        {(Number(machineHand.data?.proposals ?? 0) > 0
          || Number(machineHand.data?.resignations ?? 0) > 0) && (
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2 pb-0.5">
            {Number(machineHand.data?.proposals ?? 0) > 0 && (
              <button
                type="button"
                className="cursor-pointer rounded-sm border border-[rgba(233,164,60,0.55)] px-2 py-0.5 font-mono text-[11px] text-primary hover:bg-[rgba(233,164,60,0.08)]"
                title={t.overview.machineHand.title}
                onClick={() => navigate({ to: "/episodes", search: { section: "episodes" } })}
              >
                {t.overview.machineHand.proposals(Number(machineHand.data?.proposals ?? 0))}
              </button>
            )}
            {Number(machineHand.data?.resignations ?? 0) > 0 && (
              <button
                type="button"
                className="cursor-pointer rounded-sm border border-[rgba(201,123,18,0.5)] px-2 py-0.5 font-mono text-[11px] text-severity-warn hover:bg-[rgba(201,123,18,0.08)]"
                title={t.overview.machineHand.title}
                onClick={() => navigate({ to: "/episodes", search: { section: "episodes" } })}
              >
                {t.overview.machineHand.resignations(Number(machineHand.data?.resignations ?? 0))}
              </button>
            )}
          </div>
        )}
      </div>

      <DashboardControls
        range={range}
        layout={layout}
        trouble={trouble}
        showVolume={showVolume}
        groupBy={groupBy}
        shown={overview.shown}
        onRange={next => patch({ range: next === DEFAULT_RANGE ? undefined : next })}
        onLayout={next => patch({ layout: next === "table" ? "table" : undefined })}
        onTrouble={next => patch({ trouble: next ? true : undefined })}
        onVolume={next => patch({ volume: next ? undefined : false })}
        onGroupBy={next => patch({ groupBy: next.join(",") })}
      />

      {(missing.length > 0 || update.isError) && (
        <div className="shrink-0 border-b border-[#17293D] px-6 py-1.5 font-mono text-[11px] text-severity-warn">
          {update.isError && `${t.overview.errors.saveSubscriptions} `}
          {missing.length > 0 && t.overview.errors.unavailable(missing.join(" · "))}
        </div>
      )}

      <div className="min-h-0 flex-1 overflow-auto px-6 pt-4 pb-7">
        {catalog.data === undefined ? (
          <p className="py-16 text-center text-[13px] text-[#8CA1B8]">{t.common.loading}</p>
        ) : overview.total === 0 ? (
          <p className="py-16 text-center text-[13px] text-[#8CA1B8]">
            {t.overview.empty.noApplication}
          </p>
        ) : overview.shown === 0 ? (
          // A good result — not an error, and no "clear filter" button; the toggle is right there.
          <p className="py-16 text-center text-[13px] text-[#8CA1B8]">
            {t.overview.empty.nothingInTrouble}
          </p>
        ) : layout === "table" ? (
          <ServiceTable
            tiles={overview.groups[0]?.services ?? []}
            showVolume={showVolume}
            windowDescribed={windowDescribed}
            isSubscribed={tile => isServiceSubscribed(set, tile.applicationId, tile.serviceId)}
            isViaApplication={tile => isApplicationSubscribed(set, tile.applicationId)}
            onToggleMail={toggleMail}
            onLogs={openLogs}
            onTraces={openTraces}
            onEpisodes={openEpisodes}
          />
        ) : (
          <div className="flex flex-col gap-5">
            {overview.groups.map(group => (
              <section key={group.key}>
                <div className="flex items-center gap-2.5">
                  <h2 className="text-base font-semibold tracking-[-0.15px]">{group.key}</h2>
                  <GroupBadge inTrouble={group.inTrouble} quieted={group.quieted} />
                  <span className="font-mono text-[11px] text-[#5F7590]">
                    {t.overview.serviceCount(group.services.length)}
                  </span>
                </div>
                <div
                  data-testid="service-cards"
                  className="mt-2.5 grid grid-cols-[repeat(auto-fill,minmax(458px,1fr))] gap-3.5"
                >
                  {group.services.map(tile => (
                    <ServiceCard
                      key={tile.serviceId}
                      tile={tile}
                      showVolume={showVolume}
                      subscribed={isServiceSubscribed(set, tile.applicationId, tile.serviceId)}
                      viaApplication={isApplicationSubscribed(set, tile.applicationId)}
                      onToggleMail={() => toggleMail(tile)}
                      onLogs={() => openLogs(tile)}
                      onTraces={() => openTraces(tile)}
                      onEpisodes={() => openEpisodes(tile)}
                    />
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
