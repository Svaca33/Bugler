import type { Messages } from "../messages";
import { access } from "./access";
import { alerting } from "./alerting";
import { common } from "./common";
import { explore } from "./explore";
import { nav } from "./nav";
import { overview } from "./overview";
import { registry } from "./registry";
import { server } from "./server";
import { storage } from "./storage";
import { users } from "./users";

/** Čeština. Loaded on demand — only the language a person reads rides in their bundle. */
export const cs: Messages = {
  common,
  nav,
  access,
  users,
  explore,
  overview,
  alerting,
  registry,
  server,
  storage,
};
