/** OTel severity numbers: 1-4 TRACE, 5-8 DEBUG, 9-12 INFO, 13-16 WARN, 17-20 ERROR, 21-24 FATAL. */
export function severityLabel(severityNumber: number): string {
  if (severityNumber >= 21) return "FATAL";
  if (severityNumber >= 17) return "ERROR";
  if (severityNumber >= 13) return "WARN";
  if (severityNumber >= 9) return "INFO";
  if (severityNumber >= 5) return "DEBUG";
  if (severityNumber >= 1) return "TRACE";
  return "UNSET";
}

export function severityClass(severityNumber: number): string {
  if (severityNumber >= 17) return "text-red-500";
  if (severityNumber >= 13) return "text-amber-500";
  if (severityNumber >= 9) return "text-sky-500";
  return "text-muted-foreground";
}

/** Threshold values for the "minimum severity" filter dropdown. */
export const severityFilterOptions = [
  { value: 0, label: "All severities" },
  { value: 9, label: "Info and above" },
  { value: 13, label: "Warn and above" },
  { value: 17, label: "Error and above" },
] as const;
