import { getMessages } from "@/i18n/runtime";

/** The widest window a kind of trouble may keep — mirrors FingerprintQuietWindow.MaxMinutes. */
export const MAX_QUIET_WINDOW_MINUTES = 7 * 24 * 60;

export interface QuietWindowState {
  /** What this kind of trouble keeps for itself; null while it inherits. */
  own: number | null;
  /** What it would fall back to — the Service's resolved window. */
  inherited: number;
}

/** Minutes as the panel says them: whole hours and days read better than four digits.
 * "h" and "min" are unit abbreviations and stay as they are in every language. */
export function quietWindowWords(minutes: number): string {
  if (minutes % (24 * 60) === 0) {
    return getMessages().alerting.quietWindow.days(minutes / (24 * 60));
  }
  if (minutes % 60 === 0) {
    const hours = minutes / 60;
    return `${hours} h`;
  }
  return `${minutes} min`;
}

/**
 * The line under the field. It must never claim the window belongs to the Episode: what is saved
 * here outlives it and governs every later Episode of the same kind.
 */
export function describeQuietWindow(state: QuietWindowState): string {
  const words = getMessages().alerting.quietWindow;
  return state.own === null
    ? words.inheritedFromService(quietWindowWords(state.inherited))
    : words.ownDescription(quietWindowWords(state.own), quietWindowWords(state.inherited));
}

/** Null when the typed value may be saved; the reason it may not otherwise. */
export function quietWindowError(typed: string): string | null {
  const trimmed = typed.trim();
  if (trimmed.length === 0) {
    return null; // Empty means "inherit", which is always allowed.
  }

  if (!/^[0-9]+$/.test(trimmed)) {
    return getMessages().alerting.quietWindow.wholeMinutesOnly;
  }

  const minutes = Number(trimmed);
  if (minutes < 1 || minutes > MAX_QUIET_WINDOW_MINUTES) {
    return getMessages().alerting.quietWindow.bounds(MAX_QUIET_WINDOW_MINUTES);
  }

  return null;
}
