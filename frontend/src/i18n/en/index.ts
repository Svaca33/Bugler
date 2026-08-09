import type { Messages } from "../messages";
import { access } from "./access";
import { alerting } from "./alerting";
import { common } from "./common";
import { explore } from "./explore";
import { mcp } from "./mcp";
import { nav } from "./nav";
import { overview } from "./overview";
import { registry } from "./registry";
import { server } from "./server";
import { storage } from "./storage";
import { users } from "./users";

/** English: the language of last resort, statically bundled so there is always something to say. */
export const en: Messages = {
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
  mcp,
};
