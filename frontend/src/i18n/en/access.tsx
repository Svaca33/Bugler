import type { AccessMessages } from "../sections/access";

export const access: AccessMessages = {
  emailLabel: "E-mail",
  emailPlaceholder: "you@company.com",
  passwordsDoNotMatch: "The two do not match.",

  login: {
    title: "Sign in to Bugler",
    description: "Use your local Bugler account.",
    passwordLabel: "Password",
    staySignedIn: "Stay signed in",
    submit: "Sign in",
    submitting: "Signing in…",
    forgotPassword: "Forgot your password?",
  },

  setup: {
    title: "Welcome to Bugler",
    description: "Create the first account. It becomes the server administrator.",
    nameLabel: "Name",
    passwordLabel: "Password (min 8 characters)",
    submit: "Create admin account",
    submitting: "Creating…",
  },

  forgot: {
    title: "Forgot your password?",
    description:
      "Give the address your account uses and Bugler will mail you a link to set a new password.",
    submit: "Send the link",
    submitting: "Sending…",
    backToSignIn: "Back to sign in",
    sent: {
      title: "Check your mail",
      description:
        "If an account uses that address, a link to set a new password is on its way to it. The link works once and stops working in an hour.",
    },
    nothingArrived: askAgain => <>Nothing arrived? Wait a couple of minutes, then {askAgain}.</>,
    askAgain: "ask again",
  },

  reset: {
    title: "Set a new password",
    description:
      "Once it is set, every session of this account is signed out — sign in again with the new password.",
    newPasswordLabel: "New password (min 8 characters)",
    repeatPasswordLabel: "Repeat new password",
    submit: "Set password",
    submitting: "Setting…",
    askForNewLink: "Ask for a new link",
    missingToken: {
      title: "Something is missing from that link",
      description:
        "This address carries no reset link. Open the one from the mail, or ask for a new one.",
      askForLink: "Ask for a link",
    },
  },

  account: {
    title: "Account",
    description:
      "The language Bugler speaks to you, your password, the applications you are watching, and " +
      "the machine delegations you have issued to let tools read telemetry in your name.",
  },

  focus: {
    caption: "Focus",
    description:
      "The applications you are watching. Everything else is left out of your dashboard, logs, " +
      "traces and episodes, and its alerts stop mailing you. Nobody else's view changes, and " +
      "nothing is taken away from you — an application you leave out is hidden, not closed.",
    attendingToNothing:
      "You are watching nothing, so Bugler is showing you no telemetry at all. Tick an " +
      "application to start.",
    nothingToAttendTo: "No application is registered on this server yet.",
    saveFailed: "That did not save. Try again.",
    empty: {
      title: "You are watching nothing",
      description:
        "Bugler has telemetry, but none of it is from an application you are watching. Choose " +
        "the ones you want to see.",
      action: "Choose applications",
    },
  },

  changePassword: {
    title: "Change password",
    description:
      "Your current password proves it is you. Signing in elsewhere will need the new one.",
    changedTitle: "Password changed",
    changedDescription:
      "Everywhere else you were signed in has been signed out. This browser stays.",
    currentPasswordLabel: "Current password",
    newPasswordLabel: "New password (min 8 characters)",
    repeatPasswordLabel: "Repeat new password",
    submit: "Change password",
    submitting: "Changing…",
  },

  errors: {
    tooManyAttemptsIn: seconds => `Too many attempts. Try again in ${seconds} seconds.`,
    tooManyAttempts: "Too many attempts. Wait a moment and try again.",
    invalidCredentials: "Invalid e-mail or password.",
    loginFailed: "Login failed.",
    setupAlreadyCompleted: "Setup has already been completed.",
    setupFailed: "Setup failed — check the e-mail and password (min 8 chars).",
    passwordNotChanged: "The password was not changed.",
    resetUnavailable: "This server cannot send mail, so passwords cannot be reset by link.",
    forgotRequestFailed: "The request could not be sent.",
    passwordNotSet: "The password was not set.",
  },
};
