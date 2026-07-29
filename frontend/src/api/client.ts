import createClient from "openapi-fetch";

import type { paths } from "./schema";

/** Typed client over the Bugler REST API; cookies ride along automatically. */
export const api = createClient<paths>({ baseUrl: "/" });

export type LogRecord =
  NonNullable<paths["/api/logs/{id}"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type SearchLogsResponse =
  NonNullable<paths["/api/logs"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type TraceSummary =
  NonNullable<
    paths["/api/traces"]["get"]["responses"]["200"]["content"]["application/json"]
  >["items"][number];

export type TraceDetail =
  NonNullable<paths["/api/traces/{traceId}"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type TraceSpan = TraceDetail["spans"][number];

export type Catalog =
  NonNullable<paths["/api/catalog"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type CurrentUser =
  NonNullable<paths["/api/auth/me"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type EpisodesResponse =
  NonNullable<paths["/api/alerting/episodes"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type Episode = EpisodesResponse["items"][number];

export type Subscriptions =
  NonNullable<paths["/api/alerting/subscriptions"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type ApplicationAlerting =
  NonNullable<
    paths["/api/admin/applications/{applicationId}/alerting"]["get"]["responses"]["200"]["content"]["application/json"]
  >;

export type Sensitivity = NonNullable<ApplicationAlerting["sensitivity"]>;
