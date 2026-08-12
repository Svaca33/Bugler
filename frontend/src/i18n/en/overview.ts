import { FORMAT_LOCALES } from "../language";
import type { OverviewMessages } from "../sections/overview";

export const overview: OverviewMessages = {
  title: "Your services",
  subtitle: "Every service you can see, and whether it is in trouble right now.",
  controls: {
    windowCaption: "WINDOW",
    groupByCaption: "GROUP BY",
    troubleOnly: "Only services in trouble",
    volume: "Volume",
    facet: {
      app: "Application",
      namespace: "Namespace",
      environment: "Environment",
      service: "Service",
    },
    shown: count => `${count} shown`,
    cardsTitle: "Cards",
    tableTitle: "Table",
  },
  errors: {
    saveSubscriptions: "Failed to save subscriptions.",
    unavailable: parts => `Unavailable right now: ${parts}.`,
    volumeAggregate: "volume — counts and sparklines show nothing",
    episodesAggregate: "episodes — statuses show nothing",
  },
  empty: {
    noApplication: "No application is visible to you.",
    nothingInTrouble: "Nothing in trouble — no watched service has an open episode.",
  },
  allServices: "All services",
  serviceCount: count => (count === 1 ? `${count} service` : `${count} services`),
  badge: {
    openEpisodes: count => (count === 1 ? "open episode" : `${count} open episodes`),
    quietedEpisodes: count => (count === 1 ? "quieted episode" : `${count} quieted episodes`),
    quieted: count => `${count} quieted`,
    inTrouble: count => `${count} in trouble`,
    allClear: "all clear",
  },
  card: {
    caption: {
      errors: "ERRORS",
      warn: "WARN",
      logs: "LOGS",
    },
    now: "now",
    opened: clock => `opened ${clock}`,
    healthCheck: "health check",
    errCount: count => `${count.toLocaleString(FORMAT_LOCALES.en)} err`,
    warnCount: count => `${count.toLocaleString(FORMAT_LOCALES.en)} warn`,
  },
  actions: {
    toLogs: "To logs",
    toTraces: "To traces",
    toEpisodes: "To episodes",
  },
  mail: {
    viaApplication: application => `Subscribed through the application ${application}`,
    subscribed: "Subscribed — mail when a new episode opens",
    notSubscribed: "Not subscribed",
  },
  sparklineBar: (stamp, error, warn, logs) =>
    `${stamp} · ${error.toLocaleString(FORMAT_LOCALES.en)} err · ` +
    `${warn.toLocaleString(FORMAT_LOCALES.en)} warn · ${logs.toLocaleString(FORMAT_LOCALES.en)} logs`,
  machineHand: {
    proposals: count =>
      `${count} machine proposal${count === 1 ? "" : "s"} awaiting a verdict`,
    resignations: count =>
      `${count} machine resignation${count === 1 ? "" : "s"} — a human hand is needed`,
    title: "The machine hand awaits a person — open the episodes",
  },
  table: {
    header: {
      service: "SERVICE",
      status: "STATUS",
      errors: "ERRORS",
      warn: "WARN",
      logs: "LOGS",
      volume: "VOLUME",
      latestEpisode: "LATEST EPISODE",
      mail: "MAIL",
    },
    allClearAgo: span => `all clear ${span} ago`,
    noEpisodeIn: window => `no episode in ${window.replace(/^Last /, "")}`,
  },
};
