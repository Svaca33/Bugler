import type { ServerMessages } from "../sections/server";

export const server: ServerMessages = {
  adminShell: {
    title: "Administrace",
    subtitle: "Co posílá telemetrii a kdo ji smí číst.",
    tabs: {
      topology: "Topologie",
      storage: "Úložiště",
      people: "Lidé",
      server: "Server",
    },
  },

  page: {
    title: "Server",
    subtitle: "Zda tato instance dokáže to, co slibuje.",
  },

  language: {
    caption: "JAZYK",
    intro:
      "Čím tento server mluví, dokud si člověk nevybere: přihlašovací obrazovka, výstrahy do "
      + "chatové místnosti a všichni, kdo si vlastní jazyk nezvolili.",
    label: "Jazyk serveru",
    loadFailed: "Jazyk serveru se nepodařilo načíst.",
    saveFailed: "Jazyk se nepodařilo uložit.",
  },

  mail: {
    caption: "POŠTA",
    loading: "Načítání nastavení pošty…",
    loadFailed: "Nastavení pošty se nepodařilo načíst.",
    intro:
      "SMTP server, přes který tento Bugler odesílá — výstrahy i odkazy pro obnovu hesla. "
      + "Obyčejnému relayi stačí host a adresa odesílatele; přihlašovací údaje nechte prázdné, "
      + "pokud server žádné nevyžaduje.",
    hostLabel: "Server (název hostitele nebo IP)",
    hostPlaceholder: "např. 172.19.1.236",
    portLabel: "Port",
    usualPort: port => `Obvyklý port pro tento režim: ${port} — použít`,
    securityLabel: "Zabezpečení",
    security: {
      Automatic: "Automaticky — STARTTLS, pokud jej server nabízí",
      None: "Žádné — nešifrovaně",
      StartTls: "STARTTLS — vyžadováno",
      ImplicitTls: "TLS — implicitní (vyhrazený port)",
    },
    usernameLabel: "Uživatelské jméno (prázdné = bez přihlášení)",
    passwordLabel: "Heslo",
    passwordRemovedOnSave: "při uložení se odstraní",
    passwordSavedKeep: "uloženo — ponechte prázdné pro zachování",
    removeButton: "Odstranit",
    keepButton: "Ponechat",
    fromLabel: "Adresa odesílatele",
    saveButton: "Uložit",
    savingButton: "Ukládání…",
    storedNote: "Nakonfigurováno zde; nastavení Mail:Smtp daného nasazení se ignoruje.",
    resetButton: "Vrátit ke konfiguraci serveru",
    resettingButton: "Obnovování…",
    fromConfigurationNote: "Z konfigurace serveru — uložením se hodnoty napříště uchovají zde.",
    saveFallback: "Nastavení se nepodařilo uložit.",
    saveFailedTitle: "Nastavení nebylo uloženo.",
    resetFailed: "Obnovení neproběhlo.",
    testIntro:
      "Odešle zprávu na adresu vašeho vlastního účtu. Pokud dorazí, dojdou příjemcům "
      + "i výstrahy a odkazy pro obnovu hesla.",
    sendTestButton: "Odeslat zkušební zprávu",
    sendingButton: "Odesílání…",
    testsSavedNote: "Testuje uloženou konfiguraci — úpravy výše se projeví až po uložení.",
    sentToPrefix: "Odesláno na",
    sendRefused: "Server zprávu odmítl.",
    sendFailedTitle: "Zprávu se nepodařilo odeslat.",
  },

  ai: {
    caption: "AI",
    loading: "Načítání nastavení AI…",
    loadFailed: "Nastavení AI se nepodařilo načíst.",
    intro:
      "Model, kterého se tento Bugler smí ptát na výklad důkazů epizody. Nenastavené znamená AI "
      + "vypnutou všude — a i nastavená nevidí nic z aplikace, jejíž souhlas správce nezapnul.",
    providerLabel: "Provider",
    provider: {
      Anthropic: "Anthropic API",
      OpenAiCompatible: "OpenAI-kompatibilní endpoint (Ollama, vLLM, …)",
    },
    baseUrlLabel: "Základní adresa",
    baseUrlHelpAnthropic: "Nepovinná — prázdná znamená vlastní adresu Anthropicu.",
    baseUrlHelpOpenAi: "Povinná, včetně segmentu verze — např. http://localhost:11434/v1",
    apiKeyLabel: "API klíč",
    apiKeyRemovedOnSave: "při uložení se odstraní",
    apiKeySavedKeep: "uložen — ponechte prázdné pro zachování",
    removeButton: "Odstranit",
    keepButton: "Ponechat",
    modelLabel: "Model",
    modelPlaceholder: "např. claude-haiku-4-5 nebo llama3.1",
    patienceLabel: "Jak dlouho výstraha čeká na svůj výklad",
    patience: {
      none: "Nečekat",
      seconds: "Počet sekund",
      forever: "Jak dlouho bude třeba",
    },
    patienceSecondsLabel: "Sekundy",
    patienceHelp:
      "Výstraha, jejíž výklad se ještě píše, se zdrží nejvýše takto dlouho, pak odejde bez něj. "
      + "Výklad dopsaný pozdě se i tak objeví v detailu epizody.",
    configuredNote: "AI je zapnutá: tato nastavení dávají dohromady funkčního providera.",
    notConfiguredNote: "AI je vypnutá: nastavení je neúplné, nikdo se žádného modelu na nic neptá.",
    saveButton: "Uložit",
    savingButton: "Ukládání…",
    storedNote: "Nakonfigurováno zde; nastavení Ai daného nasazení se ignoruje.",
    resetButton: "Vrátit ke konfiguraci serveru",
    resettingButton: "Obnovování…",
    fromConfigurationNote: "Z konfigurace serveru — uložením se hodnoty napříště uchovají zde.",
    saveFallback: "Nastavení se nepodařilo uložit.",
    saveFailedTitle: "Nastavení nebylo uloženo.",
    resetFailed: "Obnovení neproběhlo.",
    testIntro:
      "Položí uloženému providerovi jednu krátkou otázku. Pokud odpověď dorazí, výklady se budou "
      + "psát pro aplikace, které daly souhlas.",
    askTestButton: "Položit zkušební otázku",
    askingButton: "Ptám se…",
    testsSavedNote: "Testuje uloženou konfiguraci — úpravy výše se projeví až po uložení.",
    answerPrefix: "Model odpověděl:",
    askRefused: "Provider otázku odmítl.",
    askFailedTitle: "Provider neodpověděl.",
  },
};
