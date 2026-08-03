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
};
