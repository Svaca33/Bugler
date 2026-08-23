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

  /**
   * What "the same trouble" means for an Application: the Fingerprint Rule (ADR 0033) and the
   * Episode Scope (ADR 0034). The only settings whose change re-partitions what is already open,
   * so the only ones that ask first and report afterwards.
   */
  groupingCard: {
    caption: string;
    ruleLabel: string;
    /**
     * Labels of the Fingerprint Rule options, keyed by their API value. There is no separate
     * "default" entry: it would name the same rung twice, so the default is simply the one an
     * untouched Application shows selected.
     */
    rule: { ThrowingCode: string; KindOfFailure: string; WhatWasSaid: string };
    attributeLabel: string;
    attributePlaceholder: string;
    scopeCaption: string;
    byEnvironment: string;
    byNamespace: string;
    byServiceName: string;
    /** The confirmation the change asks for before it saves — it is irreversible. */
    confirmTitle: string;
    confirmIntro: string;
    /** While the cost is still being counted. */
    warningCounting: string;
    /** What the change will cost: how many open episodes it Mutes. */
    warning(openEpisodes: number, capped: boolean): string;
    confirmButton: string;
    /** What it did cost, once it is done. */
    done(mutedEpisodes: number, droppedQuietWindows: number): string;
    explainer: string;
    saveFailed: string;
    countFailed: string;
  };

  /**
   * The explainer behind the `?` on the grouping card: the ladder, what a frame is once the noise
   * is stripped, and how far one episode reaches. The reader is a developer, so it goes into
   * detail the dropdown cannot.
   */
  groupingHelp: {
    title: string;
    description: string;
    ladderLabel: string;
    /** The axis beside the ladder: the top rung separates most, the bottom least. */
    finer: string;
    coarser: string;
    /** The chip on the rung that outranks the Rule, and the one on what stands by default. */
    rungAboveTheRule: string;
    rungDefault: string;
    rungAttributeTitle: string;
    rungAttributeBody: string;
    rungStackTitle: string;
    rungStackBody: string;
    rungFailureTitle: string;
    rungFailureBody: string;
    rungMessageTitle: string;
    rungMessageBody: string;
    /** Where the dropdown lands on the ladder. */
    ruleNote: string;
    /** What happens where the chosen rung cannot answer. */
    degradeNote: string;
    framesLabel: string;
    framesRawCaption: string;
    framesKeptCaption: string;
    framesNote: string;
    runtimesNote: string;
    scopeLabel: string;
    scopeAlways: string;
    byEnvironment: string;
    byNamespace: string;
    byServiceName: string;
    scopeEnvNote: string;
    scopeNsNote: string;
    scopeNameNote: string;
    scopeExample: string;
    repartitionNote: string;
    gotIt: string;
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
    /** The Machine Claim lease (Alerting CONTEXT.md: Machine Claim) — hours, empty inherits. */
    claimLeaseLabel: string;
    claimLeaseHelp: string;
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

  /** The AI Consent card on the application detail (ADR 0028). */
  aiCard: {
    caption: string;
    consentLabel: string;
    /** The promise in plain words: what leaves, and where to. Shown beside the switch, always. */
    whatLeaves: string;
    /** When the server itself has no AI configured — consent stays storable, nothing flows. */
    serverAiOffNote: string;
    saveFailed: string;
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
