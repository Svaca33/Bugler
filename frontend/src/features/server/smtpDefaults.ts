import type { components } from "@/api/schema";

export type SmtpSecurity = components["schemas"]["SmtpSecurity"];

/** The port each security mode conventionally lives on — a prefill, never an enforcement. */
export function defaultPortFor(security: SmtpSecurity): number {
  switch (security) {
    case "None":
      return 25;
    case "ImplicitTls":
      return 465;
    default:
      // Automatic and STARTTLS both belong on the submission port.
      return 587;
  }
}

/**
 * What the port field should show after the security mode changes: a port still standing on the
 * old mode's convention follows along to the new one; a port somebody typed stays put.
 */
export function portAfterSecurityChange(
  port: number,
  from: SmtpSecurity,
  to: SmtpSecurity,
): number {
  return port === defaultPortFor(from) ? defaultPortFor(to) : port;
}
