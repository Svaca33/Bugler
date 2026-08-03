/** What one of the two retention clocks is called wherever the field has to name it. */
export interface RetentionClockMessages {
  /** The field's own label. */
  label: string;
  /** How the shortening confirmation names the thing being shortened — inflected for that sentence. */
  name: string;
  /** What the confirmation says will be deleted. */
  subject: string;
}

/** The topology admin: applications → services → API keys, their retention and alerting. */
export interface RegistryMessages {
  /** The verbs the deletion flows share. */
  cancel: string;
  delete: string;

  catalog: {
    applicationsCaption: string;
    /** "3 services" — the count with its noun, pluralised. */
    serviceCount(count: number): string;
    addApplicationLabel: string;
    applicationNamePlaceholder: string;
    addButton: string;
    selectApplicationPrompt: string;
    deleteApplication: string;
    deleteApplicationTitle(name: string): string;
    deleteApplicationConsequence(serviceCount: number): string;
    deleteServiceTitle(label: string): string;
    deleteServiceConsequence: string;
    /** The SERVICES caption naming the server-default retention of both clocks. */
    servicesCaption(logDays: number, traceDays: number): string;
    addServiceCaption: string;
    namespaceLabel: string;
    namespacePlaceholder: string;
    environmentLabel: string;
    environmentPlaceholder: string;
    serviceNameLabel: string;
    serviceNamePlaceholder: string;
    addServiceButton: string;
    /** The one-process-one-registration help; names the defaults only when the server has answered. */
    addServiceHelp(defaults: { logDays: number; traceDays: number } | null): string;
  };

  keys: {
    issueButton: string;
    newKeyFor(label: string): string;
    shownOnce: string;
    copyButton: string;
    savedItButton: string;
    activeKeysCaption(count: number): string;
    revokeButton: string;
    noKeyYet: string;
  };

  alertingCard: {
    /** The card caption naming the deployment defaults. */
    caption(sensitivity: string, quietWindowMinutes: number | string): string;
    sensitivityLabel: string;
    /** Labels of the Sensitivity options, keyed by their API value. */
    sensitivity: { Off: string; Errors: string; ErrorsAndWarnings: string };
    /** The application-level "no setting" option, naming the default it falls back to. */
    defaultOption(label: string): string;
    /** The service-level "no setting" option, naming what the application resolves to. */
    inheritOption(label: string): string;
    quietWindowLabel: string;
    quietWindowHelp: string;
    explainer: string;
    webhookLabel: string;
    /** The chip on a stored webhook — only its host ever comes back. */
    webhookSet(domain: string): string;
    replaceButton: string;
    removeButton: string;
    saveButton: string;
    saveFailed: string;
    webhookInvalid: string;
    overrideSaveFailed: string;
    logsWatch: string;
    healthCheckWatch: string;
    healthCheckUrlLabel: string;
    healthCheckAnswered: string;
    healthCheckNoAnswer: string;
    /** The health-check help around the literal `localhost`, which stays code. */
    healthCheckHelpBeforeCode: string;
    healthCheckHelpAfterCode: string;
  };

  retention: {
    logs: RetentionClockMessages;
    traces: RetentionClockMessages;
    shortenTitle(name: string, days: number): string;
    /** Trailing space: the purge consequence follows in the same paragraph. */
    followsDefault(days: number): string;
    purgeConsequence(subject: string, days: number): string;
    saveFailedUnchanged: string;
    shortenButton: string;
    saveFailed: string;
  };

  deleteDialog: {
    cannotBeUndone: string;
    /** Around the phrase to type back, which renders as code between the two. */
    typeBeforePhrase: string;
    typeAfterPhrase: string;
    failed: string;
  };
}
