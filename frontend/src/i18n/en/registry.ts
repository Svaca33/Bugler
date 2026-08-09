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
