import type { ServerMessages } from "../sections/server";

export const server: ServerMessages = {
  adminShell: {
    title: "Administration",
    subtitle: "What sends telemetry, and who may read it.",
    tabs: {
      topology: "Topology",
      storage: "Storage",
      people: "People",
      server: "Server",
    },
  },

  page: {
    title: "Server",
    subtitle: "Whether this deployment can do what it promises.",
  },

  language: {
    caption: "LANGUAGE",
    intro:
      "What this server speaks by default: the sign-in screen, alerts to a chat room, and "
      + "everyone who hasn't chosen a language of their own.",
    label: "Server language",
    loadFailed: "The server language could not be loaded.",
    saveFailed: "The language was not saved.",
  },

  mail: {
    caption: "MAIL",
    loading: "Loading the mail settings…",
    loadFailed: "The mail settings could not be loaded.",
    intro:
      "The SMTP server this Bugler sends through — alerts and password-reset links alike. A host "
      + "and a From address are all a plain relay needs; leave the credentials empty for a server "
      + "that asks for none.",
    hostLabel: "Server (host name or IP)",
    hostPlaceholder: "e.g. 172.19.1.236",
    portLabel: "Port",
    usualPort: port => `Usual port for this mode: ${port} — use it`,
    securityLabel: "Security",
    security: {
      Automatic: "Automatic — STARTTLS if the server offers it",
      None: "None — plaintext",
      StartTls: "STARTTLS — required",
      ImplicitTls: "TLS — implicit (dedicated port)",
    },
    usernameLabel: "Username (empty = no sign-in)",
    passwordLabel: "Password",
    passwordRemovedOnSave: "removed on save",
    passwordSavedKeep: "saved — leave blank to keep",
    removeButton: "Remove",
    keepButton: "Keep it",
    fromLabel: "From address",
    saveButton: "Save",
    savingButton: "Saving…",
    storedNote: "Configured here; the deployment's Mail:Smtp settings are ignored.",
    resetButton: "Reset to server configuration",
    resettingButton: "Resetting…",
    fromConfigurationNote: "From the server configuration — saving stores it here instead.",
    saveFallback: "The settings could not be saved.",
    saveFailedTitle: "The settings were not saved.",
    resetFailed: "The reset did not go through.",
    testIntro:
      "Sends a message to your own account address. If it arrives, alerts and password-reset "
      + "links will reach their recipients too.",
    sendTestButton: "Send a test message",
    sendingButton: "Sending…",
    testsSavedNote: "Tests the saved configuration — the edits above ride along only once saved.",
    sentToPrefix: "Sent to",
    sendRefused: "The server refused the message.",
    sendFailedTitle: "The message could not be sent.",
  },
};
