/**
 * The Dashboard: the service board's heading, controls, group badges, cards and table. Severity
 * band names and their abbreviations (ERRORS, WARN, err) are the domain's vocabulary and stay
 * untranslated — only the prose around them is a language's.
 */
export interface OverviewMessages {
  title: string;
  subtitle: string;
  controls: {
    /** The mono caption before the window presets. */
    windowCaption: string;
    /** The mono caption before the grouping chips. */
    groupByCaption: string;
    troubleOnly: string;
    volume: string;
    facet: {
      app: string;
      namespace: string;
      environment: string;
      service: string;
    };
    /** "7 shown" — tiles on the board after the trouble filter. */
    shown(count: number): string;
    /** Tooltip on the cards-layout toggle. */
    cardsTitle: string;
    /** Tooltip on the table-layout toggle. */
    tableTitle: string;
  };
  errors: {
    saveSubscriptions: string;
    /** "Unavailable right now: …" — the failed aggregates arrive pre-joined by " · ". */
    unavailable(parts: string): string;
    volumeAggregate: string;
    episodesAggregate: string;
  };
  empty: {
    noApplication: string;
    /** The good empty state behind the trouble filter — a result, not an error. */
    nothingInTrouble: string;
  };
  /** The group heading when nothing groups the board. */
  allServices: string;
  /** "3 services" — the count beside a group heading. */
  serviceCount(count: number): string;
  badge: {
    /** "open episode" / "3 open episodes" — the trouble chip. */
    openEpisodes(count: number): string;
    /** "quieted episode" / "3 quieted episodes" — settled trouble nobody has looked at yet. */
    quietedEpisodes(count: number): string;
    /** "3 quieted" — appended to the open chip, and the group heading's quieted count. */
    quieted(count: number): string;
    /** "2 in trouble" — the group heading's worst-state chip. */
    inTrouble(count: number): string;
    allClear: string;
  };
  card: {
    caption: {
      errors: string;
      warn: string;
      logs: string;
    };
    /** The right edge of the sparkline's axis. */
    now: string;
    /** "opened 12:04:41" — when the quoted episode opened. */
    opened(clock: string): string;
    /** Quoted when a HealthCheck watch opened the episode — nothing was logged to count. */
    healthCheck: string;
    /** "12 err" — the quoted episode's error count. */
    errCount(count: number): string;
    /** "3 warn" — the quoted episode's warn count. */
    warnCount(count: number): string;
  };
  /** The hand-off trio the cards and the table rows share. */
  actions: {
    toLogs: string;
    toTraces: string;
    toEpisodes: string;
  };
  mail: {
    /** Tooltip when the service is covered by an application-wide subscription. */
    viaApplication(application: string): string;
    subscribed: string;
    notSubscribed: string;
  };
  /** A sparkline bar's tooltip: the bucket's stamp and its counts. */
  sparklineBar(stamp: string, error: number, warn: number, logs: number): string;
  /** The machine hand's standing marks, chip-linked in the header when any await a person. */
  machineHand: {
    proposals(count: number): string;
    resignations(count: number): string;
    /** The chips' shared tooltip — they open the Episodes page. */
    title: string;
  };
  table: {
    header: {
      service: string;
      status: string;
      errors: string;
      warn: string;
      logs: string;
      volume: string;
      latestEpisode: string;
      mail: string;
    };
    /** "all clear 42 min ago" — the latest episode closed that long ago. */
    allClearAgo(span: string): string;
    /** "no episode in 24 h" — given the described window ("Last 24 h"), each language trims its own prefix. */
    noEpisodeIn(window: string): string;
  };
}
