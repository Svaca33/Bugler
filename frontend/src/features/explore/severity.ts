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
  if (severityNumber >= 17) return "text-[#F0685A]";
  if (severityNumber >= 13) return "text-primary";
  if (severityNumber >= 9) return "text-[#9FB5CA]";
  return "text-[#6E86A0]";
}

/** Severity rail (the 3px bar at the left edge of a log row). */
export function severityRailClass(severityNumber: number): string {
  if (severityNumber >= 17) return "bg-destructive";
  if (severityNumber >= 13) return "bg-[#C97B12]";
  if (severityNumber >= 9) return "bg-[#274563]";
  return "bg-[#1E344C]";
}

/** Threshold values for the "minimum severity" filter dropdown. */
export const severityFilterOptions = [
  { value: 0, label: "All severities" },
  { value: 9, label: "Info and above" },
  { value: 13, label: "Warn and above" },
  { value: 17, label: "Error and above" },
] as const;
