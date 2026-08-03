import { en } from "./en";
import { FORMAT_LOCALES, type LanguageCode } from "./language";
import type { Messages } from "./messages";

/**
 * The active language as a module-level fact, beside the React tree rather than in it: the API
 * client stamps it on every request as Accept-Language (the server answers in the language the
 * UI is speaking — ADR 0024), and formatters or query functions in plain .ts modules read the
 * catalog without threading a parameter through every call site. Only the LanguageProvider
 * activates a language; tests may too.
 */
let activeCode: LanguageCode = "en";
let activeMessages: Messages = en;

export function getActiveLanguage(): LanguageCode {
  return activeCode;
}

export function getMessages(): Messages {
  return activeMessages;
}

/** The Intl locale dates and numbers render in — the active language's, never the browser's. */
export function getFormatLocale(): string {
  return FORMAT_LOCALES[activeCode];
}

export function activate(code: LanguageCode, messages: Messages): void {
  activeCode = code;
  activeMessages = messages;
}
