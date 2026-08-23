import type { ReactNode } from "react";

/** Everything the doors say: signing in, claiming a fresh server, resetting and changing passwords. */
export interface AccessMessages {
  emailLabel: string;
  /** An example address, not a sentence — languages keep it as an address. */
  emailPlaceholder: string;
  /** Shown under both the reset form and the change dialog when the two new passwords differ. */
  passwordsDoNotMatch: string;

  login: {
    title: string;
    description: string;
    passwordLabel: string;
    staySignedIn: string;
    submit: string;
    submitting: string;
    forgotPassword: string;
  };

  setup: {
    title: string;
    description: string;
    nameLabel: string;
    passwordLabel: string;
    submit: string;
    submitting: string;
  };

  forgot: {
    title: string;
    description: string;
    submit: string;
    submitting: string;
    backToSignIn: string;
    sent: {
      title: string;
      description: string;
    };
    /** "Nothing arrived? …" with the ask-again action sitting mid-sentence. */
    nothingArrived(askAgain: ReactNode): ReactNode;
    askAgain: string;
  };

  reset: {
    title: string;
    description: string;
    newPasswordLabel: string;
    repeatPasswordLabel: string;
    submit: string;
    submitting: string;
    askForNewLink: string;
    missingToken: {
      title: string;
      description: string;
      askForLink: string;
    };
  };

  /** The dialog behind your e-mail in the header: language and password in one place. */
  account: {
    title: string;
    description: string;
  };

  /**
   * The Focus: which Applications a person attends to. A Focus is otherwise silent, so these are
   * the only two places it speaks — the card where it is chosen, and the canvas of every reading
   * page while it holds nothing at all.
   */
  focus: {
    caption: string;
    description: string;
    /** Nothing ticked, said where the ticking happens rather than in a banner over the app. */
    attendingToNothing: string;
    /** No Application is registered yet, so there is nothing to attend to either way. */
    nothingToAttendTo: string;
    saveFailed: string;
    empty: {
      title: string;
      description: string;
      action: string;
    };
  };

  changePassword: {
    title: string;
    description: string;
    changedTitle: string;
    changedDescription: string;
    currentPasswordLabel: string;
    newPasswordLabel: string;
    repeatPasswordLabel: string;
    submit: string;
    submitting: string;
  };

  /** Client-side fallbacks only — a reason the server spells out is shown verbatim instead. */
  errors: {
    tooManyAttemptsIn(seconds: number): string;
    tooManyAttempts: string;
    invalidCredentials: string;
    loginFailed: string;
    setupAlreadyCompleted: string;
    setupFailed: string;
    passwordNotChanged: string;
    resetUnavailable: string;
    forgotRequestFailed: string;
    passwordNotSet: string;
  };
}
