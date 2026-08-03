/**
 * The Explore read path: the log list, the traces list, one trace's waterfall, the Volume chart
 * above the logs, and the Time / Source / Attribute filters they all share. Severity Band names
 * (Error, WARN, FATAL…), OTel span-kind names, unit abbreviations (ms, s, min, h, d) and the
 * `res:` scope prefix are the domain's vocabulary and stay untranslated.
 */
export interface ExploreMessages {
  /** aria-label and tooltip of the round refresh button on both lists. */
  refresh: string;

  /** Messages thrown when a query fails; surfaced by the UI as `error.message`. */
  loadFailed: {
    logs: string;
    logCount: string;
    logRecord: string;
    traces: string;
    trace: string;
    volume: string;
    observedKeys: string;
  };

  /** The filter rail on both list pages: group labels and the source selects. */
  rail: {
    scopeGroup: string;
    sourceGroup: string;
    timeGroup: string;
    severityGroup: string;
    messageGroup: string;
    statusGroup: string;
    attributesGroup: string;
    allApplications: string;
    allNamespaces: string;
    allEnvironments: string;
    allServices: string;
    /** The chip narrowing the log list to one trace; the id arrives already shortened. */
    traceScope(shortId: string): string;
    /** aria-label on that chip's remove button. */
    leaveTrace: string;
    searchPlaceholder: string;
    search: string;
    errorsOnly: string;
  };

  /** The Attribute Filter add flow and the +/− magnifiers on detail-panel leaves. */
  attributes: {
    addFilter: string;
    keyPlaceholder: string;
    loadingKeys: string;
    noKeysObserved: string;
    valuePlaceholder: string;
    add: string;
    /** aria-label of the ✕ that abandons the add flow. */
    cancelFilter: string;
    /** Combobox group headings for the two scopes an Observed Key can live in. */
    attributesScope: string;
    resourceScope: string;
    filterBy(label: string): string;
    stopFilteringBy(label: string): string;
    /** Tooltips on the + and − magnifiers. */
    filterByValueTitle: string;
    removeFilterTitle: string;
  };

  /** The log list page. */
  logs: {
    title: string;
    /** The counter while the total is still unknown: only what has been paged in. */
    loaded(count: number): string;
    /** "12 of 340 records"; a capped total reads "1000+". */
    counted(shown: number, total: number, capped: boolean): string;
    follow: string;
    following: string;
    headers: {
      time: string;
      severity: string;
      service: string;
      tenant: string;
      message: string;
    };
    loadOlder: string;
    noOlderRecords: string;
  };

  /** The panel one Log Record opens into. */
  logDetail: {
    title: string;
    rows: {
      time: string;
      severity: string;
      service: string;
      scope: string;
      span: string;
    };
    viewTrace(shortId: string): string;
  };

  /** Detail-panel section headings shared by the log and span panels. */
  sections: {
    message: string;
    attributes: string;
    resource: string;
    events: string;
  };

  /** The traces list page. */
  traces: {
    title: string;
    count(count: number): string;
    headers: {
      rootSpan: string;
      service: string;
      started: string;
      duration: string;
      spans: string;
      status: string;
    };
  };

  /** One trace's waterfall and the span detail panel. */
  traceDetail: {
    loading: string;
    notFound: string;
    spansCount(count: number): string;
    errorsCount(count: number): string;
    viewCorrelatedLogs: string;
    headers: {
      span: string;
      timeline(totalMs: string): string;
      duration: string;
    };
    rows: {
      span: string;
      parent: string;
      kind: string;
      service: string;
      start: string;
      status: string;
    };
  };

  /** The Volume chart above the log list, its tooltip, and the Release markers on it. */
  volume: {
    tooWide: string;
    /** The frame caption: window boundaries plus the shared Bucket width, e.g. "30 min". */
    windowLabel(from: string, to: string, width: string): string;
    /** aria-label of the spinner while a window summarizes. */
    summarizing: string;
    bucketPartlyInside(span: string): string;
    bucketElapsing(span: string): string;
    bucketCut(span: string): string;
    /** The tick label when several Releases share one Bucket. */
    releasesCount(count: number): string;
  };

  /** The Time Filter control and the sentences the window puts on an empty list. */
  timeFilter: {
    /** aria-label of the preset Select trigger. */
    timeRangeAria: string;
    allTime: string;
    custom: string;
    edit: string;
    last: string;
    amountAria: string;
    unitAria: string;
    apply: string;
    applyRelativeAria: string;
    applyAbsoluteAria: string;
    from: string;
    to: string;
    /** Lowercase "to" between the inline From/To fields. */
    toInline: string;
    cancelAria: string;
    endsBeforeItStarts: string;
    hint: string;
    /** The control's label for a half-open window: only one end is set. */
    fromInstant(time: string): string;
    untilInstant(time: string): string;
    /** What an empty list is missing, slotted into the sentences below. */
    subjects: {
      logRecords: string;
      traces: string;
    };
    /** `window` arrives as describeDuration's own casing; each language cases it itself. */
    noneInLast(subject: string, window: string): string;
    noneIn(subject: string, window: string): string;
    noneMatch(subject: string): string;
    widenTo: string;
  };
}
