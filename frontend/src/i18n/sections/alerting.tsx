import type { ReactNode } from "react";

/** The server's Episode lifecycle names — keys into the maps below, never themselves shown. */
type EpisodeState = "Open" | "Quieted" | "Solved" | "Muted";

/**
 * Everything the Episodes tab and its panels say: one member per sentence, grouped by surface.
 * Severity band names (Error, WARN…), "health check", unit abbreviations (min, h, d, s) and the
 * values that ride in API queries stay out — they are the domain's vocabulary, not a language's.
 */
export interface AlertingMessages {
  page: {
    title: string;
    subtitle: string;
    episodesTab: string;
    subscriptionsTab: string;
  };

  /** The left rail: group captions, choice labels, and the OPENED presets by ISO duration. */
  filters: {
    lifecycle: string;
    /** A filter of its own, beside the lifecycle boxes and never among them (CONTEXT.md: Archived). */
    archived: string;
    showArchived: string;
    whoIsOnIt: string;
    source: string;
    opened: string;
    firstLogContains: string;
    nobodyYet: string;
    acknowledgedByMe: string;
    allApplications: string;
    allNamespaces: string;
    allEnvironments: string;
    allServices: string;
    anyTime: string;
    searchPlaceholder: string;
    /** Labels of the OPENED presets, keyed by their ISO duration. */
    openedPreset: Record<string, string>;
    /** "in the last 7 d" — the footer's window phrase, keyed by the ISO duration. */
    openedPhrase: Record<string, string>;
  };

  state: {
    /** The badge's compact lowercase voice, keyed by the server's state name. */
    badge: Record<EpisodeState, string>;
    /** The rail's capitalized checkbox labels for the same states. */
    filterLabel: Record<EpisodeState, string>;
  };

  /** The episodes table: columns, day groups, the footer, and each row's meta line. */
  list: {
    columnEpisode: string;
    columnState: string;
    columnDuration: string;
    emptyFiltered: string;
    emptyNoTrouble: string;
    loadOlder: string;
    nothingOlder: string;
    loadedCount(count: number): string;
    /** "12 of 40 kinds of trouble in the last 7 d" — `window` is absent when nothing narrows. */
    countOfTotal(shown: number, total: number, window: string | undefined): string;
    /** The viewer's own name in an owner mark. */
    you: string;
    solvedBy(name: string): string;
    earlierAck(name: string): string;
    hideEarlier: string;
    showEarlier(times: number): string;
    mutedDuringEpisode: string;
    /** The meta-line mark on a filed-away row — only ever seen while the rail asks for them. */
    archived: string;
    /** "5 err" — the band abbreviation stays English in every language. */
    errCount(count: string | number): string;
    warnCount(count: string | number): string;
  };

  /** The triage band above the table: one card per Episode burning right now. */
  openNow: {
    title: string;
    summary(count: number, unheld: number, oldest: string): string;
    refreshedAgo(ago: string): string;
    youHold(time: string): string;
    heldBy(name: string, forTime: string): string;
    nobodyYet: string;
  };

  /** Which time this kind of trouble is burning — the recurrence badge on an open card. */
  recurrence: {
    firstTime: string;
    nthTime(n: number): string;
  };

  /**
   * Which Services and versions are in an Episode (see CONTEXT.md: Participation) — the answer to
   * "is it still happening on the version we just shipped, and is it every deployment or only one".
   */
  participants: {
    /** "IN 3 SERVICES" — the detail section's caption. */
    caption(count: number): string;
    columnService: string;
    columnVersion: string;
    columnLast: string;
    columnMatches: string;
    /** Where the sender declared none — the intended degradation, never a dash. */
    noVersion: string;
    firstSeen(clockText: string): string;
    /** "+2" on a row too narrow to name them all. */
    more(count: number): string;
    moreTitle(count: number): string;
  };

  /**
   * What Bugler admits about how it grouped an Episode (ADR 0033): what is not understood
   * coarsens visibly, and a folded Alert says so rather than going quiet.
   */
  grouping: {
    coarsened: string;
    coarsenedTitle: string;
    truncated: string;
    truncatedTitle: string;
    storm: string;
    stormTitle: string;
  };

  /**
   * The version a Service declared. It rides on a Participation now, from the Match's own
   * `service.version` — the Release ledger answers a different question, and still overlays the
   * volume (ADR 0016, 0034).
   */
  version: {
    on(version: string): string;
  };

  /** The right-hand detail panel: captions, the lifecycle timeline, volume and recurrence. */
  detail: {
    episodeCaption: string;
    lifecycleCaption: string;
    volumeCaption: string;
    notVisible: string;
    openInLogsLink: string;
    earlierAckByYou: string;
    earlierAckBy(name: string): string;
    /** `version` arrives pre-styled; the sentence decides where it sits. */
    versionReleasedEarlier(version: ReactNode, ago: string): ReactNode;
    openedByHealthCheck: string;
    /** The severity is the band's own name and stays untranslated inside the sentence. */
    openedByLog(severity: string): string;
    sensitivity(words: string): string;
    sensitivityWords: {
      errorsAndWarnings: string;
      errors: string;
      off: string;
    };
    alertMailed(subscribers: number): string;
    deliveryPending: string;
    /** Suffix after the mail delivery clock, joined with " · " by the component. */
    postedToChat: string;
    alertPostedToChat: string;
    stillMatchingLog(since: string): string;
    stillMatchingCheck(since: string): string;
    heldOpenNote: string;
    autoCloseNote(minutes: number): string;
    errorsCount(count: number): string;
    warningsCount(count: number): string;
    kindFirstCaption: string;
    kindEarlierCaption(earlier: number): string;
    thisOne: string;
    byName(name: string): string;
    nobody: string;
    firstOfKind: string;
    cameBack(times: number): string;
    isHistory: string;
    openIt: string;
  };

  /** The hands on the timeline, narrated from the Journal (ADR 0006) — flesh and machine. */
  journal: {
    /** Stands in for a deleted account wherever a name would go, lowercase mid-sentence. */
    formerUser: string;
    youAcknowledged: string;
    acknowledged(name: string): string;
    youTookOver: string;
    tookOver(name: string): string;
    youWithdrewYours: string;
    withdrewTheirOwn(name: string): string;
    youWithdrewThe: string;
    withdrewThe(name: string): string;
    withdrewYours(name: string): string;
    youWithdrewOf(holder: string): string;
    withdrewOf(name: string, holder: string): string;
    solvedByYou: string;
    solvedBy(name: string): string;
    youArchived: string;
    archived(name: string): string;
    youUnarchived: string;
    unarchived(name: string): string;
    /** Stands in for a machine delegation no longer here, where its name would go. */
    formerMachine: string;
    claimed(machine: string): string;
    claimRenewed(machine: string): string;
    claimReleased(machine: string): string;
    claimLapsed(machine: string): string;
    claimDisplaced(name: string, machine: string): string;
    notePinned(machine: string): string;
    proposalLaid(machine: string): string;
    proposalRejected(name: string, machine: string): string;
    resigned(machine: string): string;
    resignationDismissed(name: string, machine: string): string;
  };

  /** The machine hand's live marks and the human answers to them (Alerting CONTEXT.md). */
  machine: {
    /** Compact badges on list rows, in the state badges' lowercase voice. */
    badgeClaimed: string;
    badgeClaimedTitle(machine: string): string;
    badgeProposal: string;
    badgeProposalTitle: string;
    badgeResigned: string;
    badgeResignedTitle: string;
    /** The detail section. */
    caption: string;
    /** How a mark names its hand: the delegation, and its holder where known. */
    hand(machine: string, holder: string | null): string;
    /** Where the delegation is gone, the hand is named by this alone. */
    formerHand: string;
    claimHeld(hand: string): string;
    leaseUntil(clockText: string): string;
    withdrawClaim: string;
    noteCaption: string;
    openLink: string;
    proposalHeading: string;
    proposalLaidBy(hand: string): string;
    openPr: string;
    matchesSince(count: number): string;
    /** Under an overtaken proposal: the fix did not hold, confirming is closed. */
    overtakenNote: string;
    /** Under an overtaken resignation: the statement is history. */
    resignationOvertakenNote: string;
    confirmSolved: string;
    reject: string;
    resignationHeading: string;
    resignedBy(hand: string): string;
    dismiss: string;
    /** The open episode's tail note while a Machine Claim holds it (mirrors detail.heldOpenNote). */
    heldOpenNote: string;
    /** Client-side fallbacks; a 409's own sentence is shown verbatim. */
    rejectFailed: string;
    dismissFailed: string;
    withdrawClaimFailed: string;
  };

  /** The machine's reading of the opening evidence (see Alerting CONTEXT.md: Reading). */
  reading: {
    caption: string;
    /** The visible machine-made mark: which model wrote it. */
    writtenBy(model: string): string;
    pending: string;
    /** Failure says only that the machine gave up — the evidence stands on its own. */
    failed: string;
  };

  /** The Quiet Window a kind of trouble keeps for itself (ADR 0004). */
  quietWindow: {
    caption: string;
    badge(words: string): string;
    badgeTitle: string;
    /** Whole days as words — hours and minutes stay unit abbreviations. */
    days(days: number): string;
    inheritedFromService(words: string): string;
    /** What the override says it governs: the kind of trouble, wherever its Episode Scope reaches. */
    ownDescription(own: string, inherited: string): string;
    wholeMinutesOnly: string;
    bounds(maxMinutes: number): string;
    notSaved: string;
    fieldLabel: string;
    emptyInherits: string;
  };

  /** The one confirmation in Alerting: Solved is final. */
  solve: {
    title: string;
    verdict: string;
    stillOpen(lastMatchAgo: string): string;
    cancel: string;
  };

  /**
   * The guard on the Deletion of a kind of trouble (CONTEXT.md: Deletion): it names what is about
   * to be lost — every Episode of the kind, Journal and all — and stays disarmed until the Admin
   * types the phrase back. Permanent, so never one click away.
   */
  deleteKind: {
    title: string;
    /** "Every episode of this kind goes — 3 in all — …" */
    consequence(count: number): string;
    cannotBeUndone: string;
    /** What must be typed to arm the button; a word that can be typed on any keyboard. */
    phrase: string;
    typeBeforePhrase: string;
    typeAfterPhrase: string;
    confirm: string;
    cancel: string;
    /** The client-side fallback; a 409's own sentence is shown verbatim instead. */
    failed: string;
  };

  /**
   * The selection in the list and the one hand it offers: the same Archived mark laid on many
   * Episodes at once. The bar says how many are chosen, and afterwards which were filed and which
   * refused — a selection with an open Episode in it never half-succeeds in silence.
   */
  selection: {
    /** The row checkbox's accessible name: "Select episode: <title>". */
    selectEpisode(title: string): string;
    selectAllLoaded: string;
    selected(count: number): string;
    archiveSelected: string;
    clear: string;
    /** "3 archived." — the whole selection went through. */
    filed(count: number): string;
    /** "3 archived, 2 not — still selected. <the server's sentence>" — `reasons` are the server's
     * own refusal sentences, deduplicated, shown verbatim in whatever language it spoke. */
    filedAndRefused(filed: number, refused: number, reasons: string): string;
    dismiss: string;
  };

  /** The hands themselves — buttons shared by the band cards and the detail panel — and the
   * client-side fallbacks behind them. A 409's own sentence is shown verbatim, never these. */
  actions: {
    acknowledge: string;
    withdraw: string;
    takeOver: string;
    solve: string;
    archive: string;
    unarchive: string;
    /** The Admin's irreversible hand on the whole kind, offered only once the Episode is filed. */
    deleteKind: string;
    openInLogs: string;
    alreadySolvedNoAck: string;
    ackNotSaved: string;
    withdrawFailed: string;
    alreadySolvedByOther: string;
    verdictNotSaved: string;
    archiveFailed: string;
    unarchiveFailed: string;
  };

  subscriptions: {
    summary(services: number, applications: number): string;
    body(email: string): string;
    /** What the summary says instead of an address the server has not answered with yet. */
    accountInbox: string;
    filterPlaceholder: string;
    allServicesBadge: string;
    serviceCount(subscribed: number, total: number): string;
    sensitivityOff: string;
    nothingVisible: string;
    footer: string;
  };

  /** The first-read explainer behind the `?` — prose with inline emphasis returns ReactNode. */
  help: {
    title: string;
    description: string;
    fromTroubleLabel: string;
    step1Title: string;
    step1Body: ReactNode;
    step2Title: string;
    step2Body: ReactNode;
    step3Title: string;
    step3Body: ReactNode;
    step4Title: string;
    step4Body: ReactNode;
    howItEndsLabel: string;
    lane1Title: string;
    lane1Subtitle: string;
    lane1WindowNote: string;
    quietedMark: string;
    lane1NewEpisode: string;
    lane2Title: string;
    lane2Subtitle: string;
    lane2TookOn: string;
    lane2WouldHaveQuieted: string;
    lane2SameEpisode: string;
    fourStatesLabel: string;
    stateOpenBody: string;
    stateQuietedBody: string;
    stateSolvedBody: string;
    stateMutedBody: string;
    actionsSummary: ReactNode;
    footer: ReactNode;
    gotIt: string;
  };

  /** The day separators' and history stamps' words; clocks themselves are locale-neutral. */
  format: {
    todayUpper: string;
    yesterdayUpper: string;
    todayAt(time: string): string;
    /** "4th" in English, "4." in Czech — how the language counts an occurrence. */
    ordinal(n: number): string;
  };

  /** Thrown by query functions; surfaced wherever a query error is rendered. */
  errors: {
    loadEpisodes: string;
    loadOpenEpisodes: string;
    countEpisodes: string;
    loadEpisodeHistory: string;
    loadEpisode: string;
    loadHistory: string;
    countOpenEpisodes: string;
    loadSubscriptions: string;
    loadSensitivity: string;
    saveSubscriptions: string;
  };
}
