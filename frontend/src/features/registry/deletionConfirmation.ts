import type { ServiceFacets } from "@/lib/serviceLabel";

/**
 * What an admin types to confirm deleting a Service: its registered identity, in a form
 * that can be typed on any keyboard — unlike the displayed label, which carries a middle dot.
 */
export function serviceConfirmationPhrase(service: ServiceFacets): string {
  return `${service.namespace}/${service.environment}/${service.name}`;
}

/**
 * Whether what was typed authorises an irreversible deletion. Surrounding whitespace is
 * forgiven; nothing else is, because the point is to have named what is about to be lost.
 */
export function confirmationMatches(typed: string, expected: string): boolean {
  return typed.trim() === expected && expected !== "";
}
