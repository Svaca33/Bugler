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

  ai: {
    caption: "AI",
    loading: "Loading the AI settings…",
    loadFailed: "The AI settings could not be loaded.",
    intro:
      "The model this Bugler may ask for a reading of an episode's evidence. Left unconfigured, "
      + "AI is off everywhere — and even configured, it sees nothing from an application whose "
      + "consent an admin has not turned on.",
    providerLabel: "Provider",
    provider: {
      Anthropic: "Anthropic API",
      OpenAiCompatible: "OpenAI-compatible endpoint (Ollama, vLLM, …)",
    },
    baseUrlLabel: "Base URL",
    baseUrlHelpAnthropic: "Optional — leave empty for Anthropic's own address.",
    baseUrlHelpOpenAi: "Required, including the version segment — e.g. http://localhost:11434/v1",
    apiKeyLabel: "API key",
    apiKeyRemovedOnSave: "removed on save",
    apiKeySavedKeep: "saved — leave blank to keep",
    removeButton: "Remove",
    keepButton: "Keep it",
    modelLabel: "Model",
    modelPlaceholder: "e.g. claude-haiku-4-5 or llama3.1",
    patienceLabel: "How long an alert waits for its reading",
    patience: {
      none: "Don't wait",
      seconds: "A number of seconds",
      forever: "As long as it takes",
    },
    patienceSecondsLabel: "Seconds",
    patienceHelp:
      "An alert whose reading is still being written is held back this long, then leaves without "
      + "it. A reading finished late still reaches the episode's detail.",
    configuredNote: "AI is on: these settings amount to a working provider.",
    notConfiguredNote: "AI is off: the settings are incomplete, so nothing asks any model anything.",
    saveButton: "Save",
    savingButton: "Saving…",
    storedNote: "Configured here; the deployment's Ai settings are ignored.",
    resetButton: "Reset to server configuration",
    resettingButton: "Resetting…",
    fromConfigurationNote: "From the server configuration — saving stores it here instead.",
    saveFallback: "The settings could not be saved.",
    saveFailedTitle: "The settings were not saved.",
    resetFailed: "The reset did not go through.",
    testIntro:
      "Asks the saved provider for one short answer. If it comes back, readings will be written "
      + "for the applications that consented.",
    askTestButton: "Ask a test question",
    askingButton: "Asking…",
    testsSavedNote: "Tests the saved configuration — the edits above ride along only once saved.",
    answerPrefix: "The model answered:",
    askRefused: "The provider refused the question.",
    askFailedTitle: "The provider did not answer.",
  },
};
