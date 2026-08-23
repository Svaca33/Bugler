import createClient from "openapi-fetch";

import { getActiveLanguage } from "@/i18n/runtime";
import type { paths } from "./schema";

/** Typed client over the Bugler REST API; cookies ride along automatically. */
export const api = createClient<paths>({ baseUrl: "/" });

// Every request names the language the UI is speaking, so a refusal the UI will show verbatim
// arrives already in that language (ADR 0024). The server honours only languages it knows.
//
// And every request names itself: the server refuses a mutation without this header, because a
// page on another origin can send the Session cookie along but cannot send a header (ADR 0025).
// Set on all methods rather than only the unsafe ones — one rule, nothing to keep in step.
api.use({
  onRequest({ request }) {
    request.headers.set("Accept-Language", getActiveLanguage());
    request.headers.set("Bugler-Request", "1");
    return request;
  },
});

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

export type EpisodeCounts =
  NonNullable<paths["/api/alerting/episodes/counts"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type EpisodeDetail =
  NonNullable<
    paths["/api/alerting/episodes/{id}/detail"]["get"]["responses"]["200"]["content"]["application/json"]
  >;

export type SensitivityList =
  NonNullable<paths["/api/alerting/sensitivity"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type Subscriptions =
  NonNullable<paths["/api/alerting/subscriptions"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type ApplicationAlerting =
  NonNullable<
    paths["/api/admin/applications/{applicationId}/alerting"]["get"]["responses"]["200"]["content"]["application/json"]
  >;

export type Sensitivity = NonNullable<ApplicationAlerting["sensitivity"]>;

/** How an Application distills its kinds of trouble (see Alerting CONTEXT.md: Fingerprint Rule). */
export type FingerprintRule = NonNullable<ApplicationAlerting["defaults"]["fingerprintRule"]>;
