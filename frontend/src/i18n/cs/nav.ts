import type { NavMessages } from "../sections/nav";

export const nav: NavMessages = {
  dashboard: "Dashboard",
  logs: "Logy",
  traces: "Traces",
  episodes: "Epizody",
  admin: "Administrace",
  signOut: "Odhlásit se",
  accountButtonTitle: email => `${email} — nastavení účtu`,
  account: {
    language: "Jazyk",
    languageDefault: serverLanguage => `Výchozí jazyk serveru (${serverLanguage})`,
    languageSaveFailed: "Jazyk se nepodařilo uložit.",
  },
};
