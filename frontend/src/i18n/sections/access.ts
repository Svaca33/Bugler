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
