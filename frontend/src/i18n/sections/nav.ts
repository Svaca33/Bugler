/** The app shell: tabs, the account button, signing out. */
export interface NavMessages {
  dashboard: string;
  logs: string;
  traces: string;
  episodes: string;
  admin: string;
  signOut: string;
  /** Tooltip on the e-mail in the header — the one door to your own account. */
  accountButtonTitle(email: string): string;
  account: {
    /** Label of the language picker behind the e-mail. */
    language: string;
    /** The inherit option: follow the server, named by its current language's autonym. */
    languageDefault(serverLanguage: string): string;
    languageSaveFailed: string;
  };
}
