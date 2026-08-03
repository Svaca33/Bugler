import { getFormatLocale } from "@/i18n/runtime";

const BYTE_STEPS = ["B", "kB", "MB", "GB", "TB"] as const;

/**
 * "1.2 GB" — powers of 1024 under the labels pg_size_pretty uses, so the number an admin sees
 * here is the number the database would show them. Precision follows magnitude: three
 * significant-ish digits, never scientific notation. The decimal separator is the language's.
 */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  let value = bytes;
  let step = 0;
  while (value >= 1024 && step < BYTE_STEPS.length - 1) {
    value /= 1024;
    step += 1;
  }
  const digits = step === 0 ? 0 : value >= 100 ? 0 : value >= 10 ? 1 : 2;
  const number = value.toLocaleString(getFormatLocale(), {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
  return `${number} ${BYTE_STEPS[step]}`;
}

/** Date and clock in the language's own order, with the milliseconds a log reader lives on. */
export function formatTime(timestamp: string): string {
  const date = new Date(timestamp);
  return `${date.toLocaleDateString(getFormatLocale())} ${date.toLocaleTimeString(
    getFormatLocale(),
  )}.${date.getMilliseconds().toString().padStart(3, "0")}`;
}
