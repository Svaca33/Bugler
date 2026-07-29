import type { LogRecord } from "@/api/client";

export function tenantOf(log: LogRecord): string {
  const attributes = log.attributes as Record<string, unknown> | null;
  const tenant = attributes?.["tenant.id"];
  return typeof tenant === "string" ? tenant : "";
}
