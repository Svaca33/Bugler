import type { CurrentUser } from "@/api/client";

/**
 * What the current User may do, asked as a deed rather than as a role (ADR 0015). The server
 * decides the same question through its named policies; this is the one place the client's
 * answer lives, so roles and permissions land here and nowhere else.
 */
export function canConfigureAlerting(user: CurrentUser | null | undefined): boolean {
  return user?.isAdmin === true;
}

/** Deleting a kind of trouble for good, Journal and all (Alerting CONTEXT.md: Deletion). */
export function canDeleteKindsOfTrouble(user: CurrentUser | null | undefined): boolean {
  return user?.isAdmin === true;
}
