import type { RegistryMessages } from "../sections/registry";

export const registry: RegistryMessages = {
  cancel: "Cancel",
  delete: "Delete",

  catalog: {
    applicationsCaption: "APPLICATIONS",
    serviceCount: count => `${count} ${count === 1 ? "service" : "services"}`,
    addApplicationLabel: "Add application",
    applicationNamePlaceholder: "e.g. billing-api",
    addButton: "Add",
    selectApplicationPrompt: "Select an application to manage its services and API keys.",
    deleteApplication: "Delete application",
    deleteApplicationTitle: name => `Delete application "${name}"?`,
    deleteApplicationConsequence: serviceCount =>
      `This erases ${serviceCount} ${serviceCount === 1 ? "service" : "services"}, their API keys and every log and span they ever sent.`,
    deleteServiceTitle: label => `Delete service "${label}"?`,
    deleteServiceConsequence:
      "This erases its API keys and every log and span it ever sent. Traces it shared with other services keep their remaining spans.",
    servicesCaption: (logDays, traceDays) =>
      `SERVICES · defaults to ${logDays} days of logs and ${traceDays} of traces`,
    addServiceCaption: "ADD SERVICE",
    namespaceLabel: "Namespace (deployment)",
    namespacePlaceholder: "e.g. demo",
    environmentLabel: "Environment",
    environmentPlaceholder: "e.g. prod",
    serviceNameLabel: "Service name",
    serviceNamePlaceholder: "e.g. backend",
    addServiceButton: "Add service",
    addServiceHelp: defaults =>
      `One process, one registration: a backend and a mobile client of the same deployment are two services with their own keys. Leave either retention empty to follow the server default${
        defaults === null ? "" : ` — ${defaults.logDays} days of logs, ${defaults.traceDays} of traces`
      }.`,
  },

  keys: {
    issueButton: "Issue key",
    newKeyFor: label => `New key for ${label}`,
    shownOnce: "— shown once, copy it now",
    copyButton: "Copy",
    savedItButton: "I saved it",
    activeKeysCaption: count => `ACTIVE KEYS · ${count}`,
    revokeButton: "Revoke",
    noKeyYet: "No API key yet — this service cannot send telemetry until you issue one.",
  },

  groupingCard: {
    caption: "WHAT COUNTS AS THE SAME TROUBLE",
    ruleLabel: "Group by",
    rule: {
      ThrowingCode: "The code that threw",
      KindOfFailure: "The kind of failure",
      WhatWasSaid: "What was said",
    },
    attributeLabel: "Or by this attribute",
    attributePlaceholder: "acme.error_code",
    scopeCaption: "HOW FAR ONE EPISODE REACHES",
    byEnvironment: "Environment must match",
    byNamespace: "Namespace must match",
    byServiceName: "Service name must match",
    confirmTitle: "Regroup this application?",
    confirmIntro:
      "You are changing what counts as the same trouble here — either what a fingerprint is "
      + "distilled from, or how far one episode reaches.",
    warningCounting: "Working out what this change will cost…",
    warning: (openEpisodes, capped) =>
      openEpisodes === 0
        ? "Saving re-partitions this application's kinds of trouble. Nothing is open right now, "
          + "but every tuned quiet window will be dropped. This cannot be undone."
        : `Saving mutes ${capped ? "at least " : ""}${openEpisodes} open `
          + `${openEpisodes === 1 ? "episode" : "episodes"} and drops every tuned quiet window: `
          + "their kinds of trouble land in a partition nothing will report again. "
          + "Acknowledgements and machine claims on them fall with them. This cannot be undone.",
    confirmButton: "Regroup",
    done: (mutedEpisodes, droppedQuietWindows) =>
      `Regrouped: ${mutedEpisodes} ${mutedEpisodes === 1 ? "episode" : "episodes"} muted, `
      + `${droppedQuietWindows} tuned quiet ${droppedQuietWindows === 1 ? "window" : "windows"} dropped.`,
    explainer:
      "An episode reaches across services, so both settings are the application's and no service "
      + "overrides them. Where the throwing code cannot be read — an unknown runtime, no stack — "
      + "the grouping coarsens by itself and says so on the episode.",
    saveFailed: "Failed to save the grouping settings",
    countFailed: "Failed to count the open episodes",
  },

  groupingHelp: {
    title: "How grouping works",
    description:
      "Two settings decide it: what a kind of trouble is distilled from, and how far one episode "
      + "reaches. Both are the application's — an episode crosses services, so the ends must agree.",
    ladderLabel: "THE LADDER — WHAT A FINGERPRINT IS DISTILLED FROM",
    finer: "SEPARATES MOST",
    coarser: "SEPARATES LEAST",
    rungAboveTheRule: "above the rule",
    rungDefault: "default",
    rungAttributeTitle: "A named attribute",
    rungAttributeBody:
      "Name one and its value is the whole answer wherever a log carries it — a sender that "
      + "already knows how its troubles group beats anything Bugler can distil. Where a log does "
      + "not carry it, the rule below decides. Leave it empty to use the rule alone.",
    rungStackTitle: "The code that threw",
    rungStackBody:
      "The frames of exception.stacktrace, hashed with exception.type. Two call sites that log "
      + "the same sentence stay two kinds of trouble; one bug reached twice stays one.",
    rungFailureTitle: "The kind of failure",
    rungFailureBody:
      "exception.type and the message template, ignoring the stack. Every timeout in the "
      + "application meets in one episode, wherever it was thrown.",
    rungMessageTitle: "What was said",
    rungMessageBody:
      "The message template (Serilog's and the .NET logger's alike), the event name, or the body "
      + "with its ids and numbers blanked. Groups by the sender's choice of words, so one careless "
      + "generic sentence merges unrelated failures.",
    ruleNote:
      "“Group by” picks which rung the ladder starts on. Nothing above the chosen rung is "
      + "consulted except the named attribute, which always outranks it.",
    degradeNote:
      "What cannot be read falls one rung and says so: an unknown runtime, a stack Bugler's recipe "
      + "finds no frames in, a log with no stack at all — the episode is marked “coarser”, and "
      + "a stack too long to read whole is marked “stack cut”. Nobody ends up worse off than "
      + "before; a parser written wrong shows as visible coarseness, never as a plausible answer.",
    framesLabel: "WHAT A FRAME IS, ONCE THE NOISE IS GONE",
    framesRawCaption: "AS IT ARRIVES",
    framesKeptCaption: "WHAT IS HASHED",
    framesNote:
      "The header goes because it carries the exception's own message — here a hostname and a "
      + "transaction number, which would mint a new kind of trouble per occurrence. So do "
      + "Caused by:, “… 12 more”, Python's echoed source lines, file paths and line numbers. "
      + "Every run of digits is blanked, so a deploy that shifted a line does not split one trouble "
      + "in two, and runs of identical frames collapse, so recursion of any depth is one bug.",
    runtimesNote:
      "How a stack trace is written is each runtime's own affair, so the recipe is chosen by the "
      + "telemetry.sdk.language your SDK already sends: dotnet, java, kotlin, nodejs, webjs, "
      + "python, go, php and ruby have one. Anything else falls a rung rather than guessing.",
    scopeLabel: "HOW FAR ONE EPISODE REACHES",
    scopeAlways:
      "The application always bounds an episode. On top of it, tick the facets of the sender that "
      + "must match before two logs of one kind share an episode.",
    byEnvironment: "Environment",
    byNamespace: "Namespace",
    byServiceName: "Service name",
    scopeEnvNote:
      "Recommended. Staging and production share their code and their fingerprints; merged, a "
      + "failing test run feeds the episode forever and the production trouble never falls quiet.",
    scopeNsNote:
      "Tick it to keep tenants — or whatever your namespace names — in episodes of their own.",
    scopeNameNote:
      "Tick it to keep each role apart: the api and the worker of one deployment then never share "
      + "an episode, even on the same bug in shared code.",
    scopeExample:
      "With environment alone ticked, one bug in ten customer deployments of production is one "
      + "episode with ten participations — one alert, one acknowledgement, one verdict — while "
      + "staging keeps its own.",
    repartitionNote:
      "Changing either re-partitions what is already open: those episodes are muted and the tuned "
      + "quiet windows dropped. The card asks before it saves.",
    gotIt: "Got it",
  },

  alertingCard: {
    caption: (sensitivity, quietWindowMinutes) =>
      `ALERTING · defaults ${sensitivity} · ${quietWindowMinutes} min quiet window`,
    sensitivityLabel: "Sensitivity",
    sensitivity: {
      Off: "Off",
      Errors: "Errors",
      ErrorsAndWarnings: "Errors + warnings",
    },
    defaultOption: label => `Default (${label})`,
    inheritOption: label => `Inherit (${label})`,
    quietWindowLabel: "Quiet window (min)",
    quietWindowHelp:
      "How an episode ends: once the service logs nothing the sensitivity matches for "
      + "this many minutes, the episode closes and the all clear goes out. Every new "
      + "matching log restarts the countdown. Leave empty to use the default.",
    claimLeaseLabel: "Machine claim lease (h)",
    claimLeaseHelp:
      "How long a machine's claim on an episode holds before it wilts unless the agent renews "
      + "it — a crashed agent gives the episode back within this many hours. Leave empty to "
      + "use the default.",
    explainer:
      "Off closes open episodes immediately and silently. Who gets mailed is each person's own "
      + "choice under Episodes → Subscriptions; the webhook posts every episode of this application "
      + "to one Google Chat space.",
    webhookLabel: "Google Chat webhook",
    webhookSet: domain => `set · ${domain}`,
    replaceButton: "Replace",
    removeButton: "Remove",
    saveButton: "Save",
    saveFailed: "Failed to save alerting settings",
    webhookInvalid: "The webhook must be an absolute https URL.",
    overrideSaveFailed: "Failed to save the alerting override",
    logsWatch: "LOGS",
    healthCheckWatch: "HEALTH CHECK",
    healthCheckUrlLabel: "URL",
    healthCheckAnswered: "answered",
    healthCheckNoAnswer: "no answer",
    healthCheckHelpBeforeCode:
      "Empty means nobody asks. Anything but a 2xx — including a redirect — counts as down, and "
      + "three failures in a row open an episode.",
    healthCheckHelpAfterCode: "here means inside Bugler's own container, not your machine.",
  },

  aiCard: {
    caption: "AI",
    consentLabel: "This application's telemetry may be shown to the AI provider",
    whatLeaves:
      "When an episode opens, Bugler sends the opening log with its attributes (stack traces "
      + "included), the service's last ~25 log bodies before it, and its latest release version "
      + "to the AI provider configured on the Server tab — to write a short reading of what is "
      + "likely going on. Off until turned on here, and withdrawable at any moment.",
    serverAiOffNote: "The server has no AI configured, so nothing leaves either way — the consent "
      + "just waits.",
    saveFailed: "The consent was not saved.",
  },

  retention: {
    logs: {
      label: "Log retention (days)",
      name: "log retention",
      subject: "Logs",
    },
    traces: {
      label: "Trace retention (days)",
      name: "trace retention",
      subject: "Spans",
    },
    shortenTitle: (name, days) => `Shorten ${name} to ${days} days?`,
    followsDefault: days => `This service will follow the server default of ${days} days. `,
    purgeConsequence: (subject, days) =>
      `${subject} older than ${days} days will be permanently deleted at the next purge run. This cannot be undone.`,
    saveFailedUnchanged: "Saving failed — the retention is unchanged.",
    shortenButton: "Shorten retention",
    saveFailed: "Failed to save the retention.",
  },

  deleteDialog: {
    cannotBeUndone: "This cannot be undone.",
    typeBeforePhrase: "Type",
    typeAfterPhrase: "to confirm",
    failed: "Deletion failed — nothing was removed.",
  },
};
