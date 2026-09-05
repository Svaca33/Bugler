import type { AlertingMessages } from "../sections/alerting";

const cap = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/** "4th" — the recurrence badge counts which time this kind of trouble is burning. */
const ordinal = (n: number): string => {
  const tail = n % 100;
  const suffix = tail >= 11 && tail <= 13
    ? "th"
    : n % 10 === 1 ? "st" : n % 10 === 2 ? "nd" : n % 10 === 3 ? "rd" : "th";
  return `${n}${suffix}`;
};

export const alerting: AlertingMessages = {
  page: {
    title: "Episodes",
    subtitle: "Episodes of trouble in the services you can see, and which of them mail you.",
    episodesTab: "Episodes",
    subscriptionsTab: "Subscriptions",
  },

  filters: {
    lifecycle: "LIFECYCLE",
    archived: "ARCHIVED",
    showArchived: "Show archived",
    whoIsOnIt: "WHO IS ON IT",
    source: "SOURCE",
    opened: "OPENED",
    firstLogContains: "FIRST LOG CONTAINS",
    nobodyYet: "Nobody yet",
    acknowledgedByMe: "Acknowledged by me",
    allApplications: "All applications",
    allNamespaces: "All namespaces",
    allEnvironments: "All environments",
    allServices: "All services",
    anyTime: "Any time",
    searchPlaceholder: "timeout, deadlock…",
    openedPreset: {
      P1D: "Last 24 h",
      P7D: "Last 7 d",
      P30D: "Last 30 d",
    },
    openedPhrase: {
      P1D: "in the last 24 h",
      P7D: "in the last 7 d",
      P30D: "in the last 30 d",
    },
  },

  state: {
    badge: { Open: "open", Quieted: "quieted", Solved: "solved", Muted: "muted" },
    filterLabel: { Open: "Open", Quieted: "Quieted", Solved: "Solved", Muted: "Muted" },
  },

  list: {
    columnEpisode: "EPISODE",
    columnState: "STATE",
    columnDuration: "DURATION",
    emptyFiltered: "No episodes match the state filter.",
    emptyNoTrouble: "No episodes — no watched service has logged trouble yet.",
    loadOlder: "Load older",
    nothingOlder: "Nothing older",
    loadedCount: count => `${count} loaded`,
    countOfTotal: (shown, total, window) =>
      `${shown} of ${total} kinds of trouble${window !== undefined ? ` ${window}` : ""}`,
    you: "you",
    solvedBy: name => `solved by ${name}`,
    earlierAck: name => `earlier ack: ${name}`,
    hideEarlier: "Hide the earlier episodes",
    showEarlier: times =>
      `This kind of trouble burned ${times === 1 ? "once" : `${times} times`} before — show them`,
    mutedDuringEpisode: "alerting turned off during the episode",
    archived: "archived",
    errCount: count => `${count} err`,
    warnCount: count => `${count} warn`,
  },

  openNow: {
    title: "Open now",
    summary: (count, unheld, oldest) =>
      `${count} episode${count === 1 ? "" : "s"} · ${unheld} with nobody on it · oldest ${oldest}`,
    refreshedAgo: ago => `live · refreshed ${ago} ago`,
    youHold: time => `you hold this · ${time}`,
    heldBy: (name, forTime) => `held by ${name} · ${forTime}`,
    nobodyYet: "nobody on this yet",
  },

  recurrence: {
    firstTime: "first time",
    nthTime: n => `${ordinal(n)} time`,
  },

  participants: {
    caption: count => (count === 1 ? "IN 1 SERVICE" : `IN ${count} SERVICES`),
    columnService: "SERVICE",
    columnVersion: "VERSION",
    columnLast: "LAST",
    columnMatches: "MATCHES",
    noVersion: "no version",
    firstSeen: clockText => `First fell in at ${clockText}`,
    more: count => `+${count}`,
    moreTitle: count => (count === 1 ? "1 more service" : `${count} more services`),
  },

  grouping: {
    coarsened: "coarser",
    coarsenedTitle:
      "Bugler could not read the throwing code, so this is grouped by the kind of failure or by "
      + "what was said. Grouping is coarser here than elsewhere.",
    truncated: "stack cut",
    truncatedTitle:
      "The stack trace was too long to read whole, so its head and tail were read and the middle "
      + "dropped. The grouping may be coarser than it could be.",
    storm: "folded",
    stormTitle:
      "More kinds of trouble opened in this scope at once than anybody can read, so this "
      + "episode's alerts were folded into one digest. The episode itself is untouched.",
  },

  version: {
    on: version => `on ${version}`,
  },

  detail: {
    episodeCaption: "EPISODE",
    lifecycleCaption: "LIFECYCLE",
    volumeCaption: "VOLUME SO FAR",
    notVisible: "This episode is not visible to you — or it no longer exists.",
    openInLogsLink: "Open in logs ›",
    earlierAckByYou: "An earlier episode of this kind was acknowledged by you",
    earlierAckBy: name => `An earlier episode of this kind was acknowledged by ${name}`,
    versionReleasedEarlier: (version, ago) => <>{version} released {ago} earlier</>,
    openedByHealthCheck: "Opened by a health check that stopped answering",
    openedByLog: severity => `Opened by an ${severity} log`,
    sensitivity: words => `sensitivity ${words}`,
    sensitivityWords: {
      errorsAndWarnings: "errors + warnings",
      errors: "errors",
      off: "off",
    },
    alertMailed: subscribers => `Alert mailed to ${subscribers} subscriber${subscribers === 1 ? "" : "s"}`,
    deliveryPending: "delivery pending",
    postedToChat: "posted to Google Chat",
    alertPostedToChat: "Alert posted to Google Chat",
    stillMatchingLog: since => `Still matching — last log ${since} ago`,
    stillMatchingCheck: since => `Still matching — last failed check ${since} ago`,
    heldOpenNote: "held open by the acknowledgement — solve or withdraw to let it quiet",
    autoCloseNote: minutes => `closes on its own after ${minutes} min of quiet`,
    errorsCount: count => `${count} error${count === 1 ? "" : "s"}`,
    warningsCount: count => `${count} warning${count === 1 ? "" : "s"}`,
    kindFirstCaption: "THIS KIND OF TROUBLE · FIRST TIME",
    kindEarlierCaption: earlier => `THIS KIND OF TROUBLE · ${earlier} EARLIER`,
    thisOne: "← this one",
    byName: name => `by ${name}`,
    nobody: "nobody",
    firstOfKind: "The first episode of its kind here.",
    cameBack: times =>
      `The same kind of trouble, as far as this episode reaches — it came back `
      + `${times === 1 ? "once" : `${times} times`} before.`,
    isHistory: "This episode is history — actions belong to the newest of its kind.",
    openIt: "Open it ›",
  },

  journal: {
    formerUser: "a user no longer here",
    youAcknowledged: "You acknowledged it",
    acknowledged: name => `${cap(name)} acknowledged it`,
    youTookOver: "You took it over",
    tookOver: name => `${cap(name)} took it over`,
    youWithdrewYours: "You withdrew your acknowledgement",
    withdrewTheirOwn: name => `${name} withdrew their acknowledgement`,
    youWithdrewThe: "You withdrew the acknowledgement",
    withdrewThe: name => `${cap(name)} withdrew the acknowledgement`,
    withdrewYours: name => `${cap(name)} withdrew your acknowledgement`,
    youWithdrewOf: holder => `You withdrew ${holder}'s acknowledgement`,
    withdrewOf: (name, holder) => `${cap(name)} withdrew ${holder}'s acknowledgement`,
    solvedByYou: "Solved by you",
    solvedBy: name => `Solved by ${name}`,
    youArchived: "You archived the episode",
    archived: name => `${cap(name)} archived the episode`,
    youUnarchived: "You brought the episode back from the archive",
    unarchived: name => `${cap(name)} brought the episode back from the archive`,
    formerMachine: "unknown",
    claimed: machine => `Machine "${machine}" claimed the episode`,
    claimRenewed: machine => `Machine "${machine}" renewed its lease`,
    claimReleased: machine => `Machine "${machine}" gave the episode back`,
    claimLapsed: machine => `The claim of "${machine}" lapsed`,
    claimDisplaced: (name, machine) => `${cap(name)} withdrew the claim of "${machine}"`,
    notePinned: machine => `Machine "${machine}" pinned its note`,
    proposalLaid: machine => `Machine "${machine}" proposed: solved`,
    proposalRejected: (name, machine) => `${cap(name)} rejected the proposal of "${machine}"`,
    resigned: machine => `Machine "${machine}" resigned — not one it can fix`,
    resignationDismissed: (name, machine) =>
      `${cap(name)} swept the resignation of "${machine}" aside`,
  },

  machine: {
    badgeClaimed: "machine-held",
    badgeClaimedTitle: machine => `A machine holds this episode: ${machine}`,
    badgeProposal: "proposes: solved",
    badgeProposalTitle:
      "The machine believes the cause is fixed — confirm or reject in the detail",
    badgeResigned: "machine resigned",
    badgeResignedTitle:
      "A machine looked and says this is not one it can fix — a human hand is needed",
    caption: "MACHINE HAND",
    hand: (machine, holder) => (holder === null ? `"${machine}"` : `"${machine}" (${holder})`),
    formerHand: "a machine delegation no longer here",
    claimHeld: hand => `Machine ${hand} holds this episode.`,
    leaseUntil: clockText => `lease runs until ${clockText}`,
    withdrawClaim: "Withdraw the claim",
    noteCaption: "The machine's note",
    openLink: "Open the link ›",
    proposalHeading: "The machine proposes: solved",
    proposalLaidBy: hand => `Laid by ${hand}`,
    openPr: "Open the PR ›",
    matchesSince: count =>
      count === 0
        ? "no matches since the proposal"
        : `${count} match${count === 1 ? "" : "es"} since the proposal`,
    overtakenNote:
      "Overtaken — the trouble returned in a newer episode, so the fix did not hold. The "
      + "proposal can no longer be confirmed.",
    resignationOvertakenNote:
      "Overtaken — the kind of trouble returned in a newer episode; this statement is history.",
    confirmSolved: "Confirm — solve",
    reject: "Reject",
    resignationHeading: "The machine resigned: not one it can fix",
    resignedBy: hand => `Said by ${hand}`,
    dismiss: "Dismiss",
    heldOpenNote: "held open by the machine's claim — it can quiet only after the claim ends",
    rejectFailed: "The rejection was not saved.",
    dismissFailed: "The resignation was not dismissed.",
    withdrawClaimFailed: "The claim was not withdrawn.",
  },

  reading: {
    caption: "AI READING",
    writtenBy: model => `Written by ${model} — verify against the evidence`,
    pending: "A reading of the evidence is being written…",
    failed: "The machine gave up on this one; the evidence below stands on its own.",
  },

  quietWindow: {
    caption: "QUIET WINDOW",
    badge: words => `quiet window ${words}`,
    badgeTitle: "This kind of trouble keeps its own quiet window instead of the service's; it "
      + "governs every episode of the kind in this episode scope",
    days: days => `${days} day${days === 1 ? "" : "s"}`,
    inheritedFromService: words => `Inherited from the service: ${words}.`,
    ownDescription: (own, inherited) =>
      `${own} for this kind of trouble wherever its episode reaches — it would otherwise `
      + `inherit ${inherited}.`,
    wholeMinutesOnly: "Whole minutes only.",
    bounds: maxMinutes => `Between 1 minute and ${maxMinutes} minutes (7 days).`,
    notSaved: "The quiet window was not saved.",
    fieldLabel: "Quiet window for this kind of trouble, in minutes",
    emptyInherits: "min · empty inherits",
  },

  solve: {
    title: "Solve this Episode?",
    verdict: "Solved is the verdict that the cause was fixed. It is final — there is no unsolve.",
    stillOpen: lastMatchAgo =>
      `This Episode is still open (last match ${lastMatchAgo} ago): solving closes it on the spot, and a later matching log opens a new Episode with a new Alert.`,
    cancel: "Cancel",
  },

  deleteKind: {
    title: "Delete this kind of trouble for good?",
    consequence: count =>
      `Every episode of this kind goes — ${count} in all, across every service that fed them — with its journal, participations, readings and any messages still owed. Who acknowledged and who solved it will be gone with them, and nothing about this kind will be counted or shown again.`,
    cannotBeUndone: "This cannot be undone.",
    phrase: "delete",
    typeBeforePhrase: "Type",
    typeAfterPhrase: "to confirm",
    confirm: "Delete for good",
    cancel: "Cancel",
    failed: "The kind of trouble was not deleted — nothing was removed.",
  },

  selection: {
    selectEpisode: title => `Select episode: ${title}`,
    selectAllLoaded: "Select every loaded episode",
    selected: count => `${count} selected`,
    archiveSelected: "Archive selected",
    clear: "Clear selection",
    filed: count => `${count} archived.`,
    filedAndRefused: (filed, refused, reasons) =>
      `${filed} archived, ${refused} not — still selected. ${reasons}`,
    dismiss: "Dismiss",
  },

  actions: {
    acknowledge: "Acknowledge",
    withdraw: "Withdraw",
    takeOver: "Take over",
    solve: "Solve",
    archive: "Archive",
    unarchive: "Unarchive",
    deleteKind: "Delete kind…",
    openInLogs: "Open in logs",
    alreadySolvedNoAck: "Already solved — a Solved Episode is never acknowledged.",
    ackNotSaved: "The acknowledgement was not saved.",
    withdrawFailed: "The acknowledgement was not withdrawn.",
    alreadySolvedByOther: "Already solved by someone else.",
    verdictNotSaved: "The verdict was not saved.",
    archiveFailed: "The episode was not archived.",
    unarchiveFailed: "The episode was not brought back from the archive.",
  },

  subscriptions: {
    summary: (services, applications) =>
      `You are mailed about ${services} service${services === 1 ? "" : "s"} in ${applications} application${applications === 1 ? "" : "s"}`,
    body: email =>
      `Mail lands in ${email} when a subscribed service opens a new episode — there is no mail when it closes. Subscribing an application covers all its services — including ones registered later.`,
    accountInbox: "your account inbox",
    filterPlaceholder: "Filter applications and services",
    allServicesBadge: "ALL SERVICES, PRESENT AND FUTURE",
    serviceCount: (subscribed, total) => `${subscribed} OF ${total} SERVICES`,
    sensitivityOff: "SENSITIVITY OFF",
    nothingVisible: "Nothing to subscribe to — no application is visible to you.",
    footer:
      "Mail is your own choice; the Google Chat webhook of an application posts every episode regardless of who is subscribed. Detection itself — sensitivity and the quiet window — lives in Admin → Topology.",
  },

  help: {
    title: "How episodes work",
    description:
      "One episode is one kind of trouble in one episode scope — from what opens it to the hand "
      + "that ends it. A scope is the application plus whichever facets of the sender an admin "
      + "says must match, so several services can be in one episode.",
    fromTroubleLabel: "FROM TROUBLE TO ALERT",
    step1Title: "Something goes wrong",
    step1Body: (
      <>
        An error log — or a <span className="text-[#DCE8F3]">health check</span> that stops
        answering, three tries running.
      </>
    ),
    step2Title: "It gets a fingerprint",
    step2Body: (
      <>
        Distilled from <span className="text-[#DCE8F3]">the code that threw</span> — the stack&apos;s
        frames, not the words. What cannot be read coarsens, and the episode says so.
      </>
    ),
    step3Title: "An episode opens",
    step3Body: (
      <>
        Only if none of that kind is open <span className="text-[#DCE8F3]">in its scope</span>.
        Otherwise it feeds the open one, and its service joins as a participation.
      </>
    ),
    step4Title: "One alert goes out",
    step4Body: (
      <>
        <span className="text-[#DCE8F3]">Once</span> per recipient, never once per subscription. A
        service falling in later tells its own followers since when.
      </>
    ),
    howItEndsLabel: "AND HOW IT ENDS",
    lane1Title: "Nobody takes it",
    lane1Subtitle: "it quiets on its own",
    lane1WindowNote: "quiet window · nothing new",
    quietedMark: "QUIETED",
    lane1NewEpisode: "a new episode · mails again",
    lane2Title: "You acknowledge it",
    lane2Subtitle: "it stays open until you solve it",
    lane2TookOn: "you took it on · 12:44",
    lane2WouldHaveQuieted: "would have quieted here",
    lane2SameEpisode: "same episode · no second mail",
    fourStatesLabel: "THE FOUR STATES",
    stateOpenBody: "Still taking matches.",
    stateQuietedBody: "Went silent on its own. Nothing is claimed fixed.",
    stateSolvedBody: "Somebody ruled the cause fixed. Final.",
    stateMutedBody: "The watch was turned off mid-episode.",
    actionsSummary: (
      <>
        <span className="text-[#DCE8F3]">Acknowledge</span> takes the trouble on ·{" "}
        <span className="text-[#DCE8F3]">Take over</span> pulls it from whoever holds it ·{" "}
        <span className="text-[#DCE8F3]">Solve</span> ends it and clears every hand. All three
        land on the newest episode of that kind only.
      </>
    ),
    footer: (
      <>
        Who gets mailed is your own choice in{" "}
        <span className="text-[#A9BDD1]">Subscriptions</span> · sensitivity and the quiet window
        live in <span className="text-[#A9BDD1]">Admin → Topology</span>
      </>
    ),
    gotIt: "Got it",
  },

  format: {
    todayUpper: "TODAY",
    yesterdayUpper: "YESTERDAY",
    todayAt: time => `today ${time}`,
    ordinal,
  },

  errors: {
    loadEpisodes: "Failed to load episodes",
    loadOpenEpisodes: "Failed to load open episodes",
    countEpisodes: "Failed to count episodes",
    loadEpisodeHistory: "Failed to load the episode history",
    loadEpisode: "Failed to load the episode",
    loadHistory: "Failed to load the history",
    countOpenEpisodes: "Failed to count open episodes",
    loadSubscriptions: "Failed to load subscriptions",
    loadSensitivity: "Failed to load alerting sensitivity",
    saveSubscriptions: "Failed to save subscriptions",
  },
};
