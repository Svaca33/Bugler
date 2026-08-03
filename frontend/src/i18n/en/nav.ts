import type { NavMessages } from "../sections/nav";

export const nav: NavMessages = {
  dashboard: "Dashboard",
  logs: "Logs",
  traces: "Traces",
  episodes: "Episodes",
  admin: "Admin",
  signOut: "Sign out",
  accountButtonTitle: email => `${email} — account settings`,
  account: {
    language: "Language",
    languageDefault: serverLanguage => `Server default (${serverLanguage})`,
    languageSaveFailed: "The language was not saved.",
  },
};
