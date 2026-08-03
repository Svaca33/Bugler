/**
 * The languages Bugler speaks. Text and formats travel together: each language names the one
 * formatting locale its dates and numbers render in, so there is exactly one axis to choose on
 * (ADR 0024). Adding a language is one entry here, one catalog per language directory, and one
 * arm in the loaders of `@/i18n` — the compiler then insists the catalog is complete.
 */
export const LANGUAGES = ["en", "cs"] as const;

export type LanguageCode = (typeof LANGUAGES)[number];

/** en keeps the en-GB rendering the UI always had; a language's formats never follow the browser. */
export const FORMAT_LOCALES: Record<LanguageCode, string> = {
  en: "en-GB",
  cs: "cs-CZ",
};

/** Each language names itself — a picker must be readable by the person who needs it most. */
export const LANGUAGE_AUTONYMS: Record<LanguageCode, string> = {
  en: "English",
  cs: "Čeština",
};

/** Recognises a supported code ("cs") or a full tag ("cs-CZ"); anything else is nobody's language. */
export function toLanguage(code: string | null | undefined): LanguageCode | null {
  if (code == null) return null;
  const primary = code.trim().toLowerCase().split("-")[0] ?? "";
  return (LANGUAGES as readonly string[]).includes(primary) ? (primary as LanguageCode) : null;
}
