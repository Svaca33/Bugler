import type { LogRecord } from "@/api/client";

export function formatTime(timestamp: string): string {
  const date = new Date(timestamp);
  return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}.${date
    .getMilliseconds()
    .toString()
    .padStart(3, "0")}`;
}

export function tenantOf(log: LogRecord): string {
  const attributes = log.attributes as Record<string, unknown> | null;
  const tenant = attributes?.["tenant.id"];
  return typeof tenant === "string" ? tenant : "";
}
