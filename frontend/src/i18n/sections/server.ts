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
}
