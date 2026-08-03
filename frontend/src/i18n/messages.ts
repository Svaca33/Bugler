import type { AccessMessages } from "./sections/access";
import type { AlertingMessages } from "./sections/alerting";
import type { CommonMessages } from "./sections/common";
import type { ExploreMessages } from "./sections/explore";
import type { NavMessages } from "./sections/nav";
import type { OverviewMessages } from "./sections/overview";
import type { RegistryMessages } from "./sections/registry";
import type { ServerMessages } from "./sections/server";
import type { StorageMessages } from "./sections/storage";
import type { UsersMessages } from "./sections/users";

/**
 * Everything the UI says, one member per sentence, grouped by feature. Each language implements
 * this whole interface in its own directory — `bun run typecheck` is what makes "this language
 * is complete" a fact rather than a hope, which is the entire mechanism (ADR 0024): a translation
 * an AI forgot is a compile error, not a silent English fallback.
 */
export interface Messages {
  common: CommonMessages;
  nav: NavMessages;
  access: AccessMessages;
  users: UsersMessages;
  explore: ExploreMessages;
  overview: OverviewMessages;
  alerting: AlertingMessages;
  registry: RegistryMessages;
  server: ServerMessages;
  storage: StorageMessages;
}
