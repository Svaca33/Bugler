/**
 * Retention as the purge sees it: the Service's own override where it has one, the server
 * default where it has none.
 */
export function effectiveRetentionDays(override: number | null, defaultDays: number): number {
  return override ?? defaultDays;
}

/**
 * Whether saving `next` hands the purge telemetry that the current policy still keeps.
 * Judged on the effective days, never on the field: clearing an override of 90 down to a
 * 30-day default takes four fifths of the history away just as surely as typing 30 does.
 */
export function shortensRetention(
  current: number | null,
  next: number | null,
  defaultDays: number,
): boolean {
  return effectiveRetentionDays(next, defaultDays) < effectiveRetentionDays(current, defaultDays);
}

/** An empty field is the server default; anything that is not whole days is not a policy. */
export type RetentionInput = { valid: true; days: number | null } | { valid: false };

export function parseRetentionInput(raw: string): RetentionInput {
  const trimmed = raw.trim();
  if (trimmed === "") {
    return { valid: true, days: null };
  }

  const days = Number(trimmed);
  return Number.isInteger(days) && days >= 1 ? { valid: true, days } : { valid: false };
}
