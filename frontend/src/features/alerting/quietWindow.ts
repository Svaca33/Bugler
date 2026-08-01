/** The widest window a kind of trouble may keep — mirrors FingerprintQuietWindow.MaxMinutes. */
export const MAX_QUIET_WINDOW_MINUTES = 7 * 24 * 60;

export interface QuietWindowState {
  /** What this kind of trouble keeps for itself; null while it inherits. */
  own: number | null;
  /** What it would fall back to — the Service's resolved window. */
  inherited: number;
}

/** Minutes as the panel says them: whole hours and days read better than four digits. */
export function quietWindowWords(minutes: number): string {
  if (minutes % (24 * 60) === 0) {
    const days = minutes / (24 * 60);
    return `${days} day${days === 1 ? "" : "s"}`;
  }
  if (minutes % 60 === 0) {
    const hours = minutes / 60;
    return `${hours} h`;
  }
  return `${minutes} min`;
}

/**
 * The line under the field. It must never claim the window belongs to the Episode: what is set
 * belongs to this kind of trouble in this Service and governs the Episodes still to come.
 */
export function describeQuietWindow(state: QuietWindowState): string {
  return state.own === null
    ? `Inherited from the service: ${quietWindowWords(state.inherited)}.`
    : `${quietWindowWords(state.own)} for this kind of trouble in this service — `
      + `it would otherwise inherit ${quietWindowWords(state.inherited)}.`;
}

/** Null when the typed value may be saved; the reason it may not otherwise. */
export function quietWindowError(typed: string): string | null {
  const trimmed = typed.trim();
  if (trimmed.length === 0) {
    return null; // Empty means "inherit", which is always allowed.
  }

  if (!/^[0-9]+$/.test(trimmed)) {
    return "Whole minutes only.";
  }

  const minutes = Number(trimmed);
  if (minutes < 1 || minutes > MAX_QUIET_WINDOW_MINUTES) {
    return `Between 1 minute and ${MAX_QUIET_WINDOW_MINUTES} minutes (7 days).`;
  }

  return null;
}
