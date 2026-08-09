/** The Server admin tab and the administration shell around all four tabs. */
export interface ServerMessages {
  /** The frame of /admin: its heading and the four section tabs. */
  adminShell: {
    title: string;
    subtitle: string;
    tabs: {
      topology: string;
      storage: string;
      people: string;
      server: string;
    };
  };

  page: {
    title: string;
    subtitle: string;
  };

  /** The server's language card: what this deployment speaks by default (ADR 0024). */
  language: {
    caption: string;
    intro: string;
    label: string;
    loadFailed: string;
    saveFailed: string;
  };

  mail: {
    caption: string;
    loading: string;
    loadFailed: string;
    intro: string;
    hostLabel: string;
    hostPlaceholder: string;
    portLabel: string;
    /** The one-click offer of the mode's conventional port. */
    usualPort(port: number): string;
    securityLabel: string;
    /** Labels of the SMTP security modes, keyed by their API value. */
    security: { Automatic: string; None: string; StartTls: string; ImplicitTls: string };
    usernameLabel: string;
    passwordLabel: string;
    passwordRemovedOnSave: string;
    passwordSavedKeep: string;
    removeButton: string;
    keepButton: string;
    fromLabel: string;
    saveButton: string;
    savingButton: string;
    storedNote: string;
    resetButton: string;
    resettingButton: string;
    fromConfigurationNote: string;
    /** Client fallback when a failed save carries no ProblemDetails. */
    saveFallback: string;
    saveFailedTitle: string;
    /** One sentence for both the ErrorNote title and the client fallback of a failed reset. */
    resetFailed: string;
    testIntro: string;
    sendTestButton: string;
    sendingButton: string;
    testsSavedNote: string;
    /** Before the address the test went to, which renders as code after it. */
    sentToPrefix: string;
    /** Client fallback when a refused test message carries no ProblemDetails. */
    sendRefused: string;
    sendFailedTitle: string;
  };

  /** The AI provider card (ADR 0027) — the SMTP card's twin, plus the Alert's patience. */
  ai: {
    caption: string;
    loading: string;
    loadFailed: string;
    intro: string;
    providerLabel: string;
    /** Labels of the providers, keyed by their API value. */
    provider: { Anthropic: string; OpenAiCompatible: string };
    baseUrlLabel: string;
    /** Under the base URL per provider: optional override vs. required, /v1 included. */
    baseUrlHelpAnthropic: string;
    baseUrlHelpOpenAi: string;
    apiKeyLabel: string;
    apiKeyRemovedOnSave: string;
    apiKeySavedKeep: string;
    removeButton: string;
    keepButton: string;
    modelLabel: string;
    modelPlaceholder: string;
    /** How long an Alert holds the door for its Reading (Alerting ADR 0009). */
    patienceLabel: string;
    patience: { none: string; seconds: string; forever: string };
    patienceSecondsLabel: string;
    patienceHelp: string;
    /** The card's verdict chips: whether the saved settings amount to AI being on at all. */
    configuredNote: string;
    notConfiguredNote: string;
    saveButton: string;
    savingButton: string;
    storedNote: string;
    resetButton: string;
    resettingButton: string;
    fromConfigurationNote: string;
    saveFallback: string;
    saveFailedTitle: string;
    resetFailed: string;
    testIntro: string;
    askTestButton: string;
    askingButton: string;
    testsSavedNote: string;
    /** Before the model's quoted answer. */
    answerPrefix: string;
    askRefused: string;
    askFailedTitle: string;
  };
}
