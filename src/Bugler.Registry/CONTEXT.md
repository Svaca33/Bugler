# Registry

The source of truth for what sends telemetry into Bugler: the catalog of applications and the services reporting on their behalf, the API keys that authenticate them, and how long their telemetry is kept.

## Language

**Application**:
A product whose telemetry Bugler collects (e.g. an e-shop, a CRM). The unit at which users are granted read access.
_Avoid_: project, app, system, product

**Service**:
A registered sender of telemetry — one role of one deployment of an Application, such as the backend of a customer's production or the mobile client talking to it. Identified by its Service Namespace, Environment and Service Name; owns its API Keys and its retention.
_Avoid_: instance, deployment, process, source

**Service Namespace**:
Which deployment of an Application a Service belongs to, usually the customer it was installed for (OTel `service.namespace`). Registered, never taken from telemetry.
_Avoid_: customer, tenant, group

**Environment**:
The stage a Service runs in — production, staging (OTel `deployment.environment.name`). Registered, never taken from telemetry.
_Avoid_: stage, tier, ring

**Service Name**:
The role a Service plays inside its deployment, e.g. backend or mobile (OTel `service.name`). Registered; what a sender calls itself carries no weight.
_Avoid_: component, module, process name

**Replica**:
One running process of a Service; several share a single registration and a single API Key. Exists only as an attribute value discovered from telemetry (OTel `service.instance.id`) — never registered.
_Avoid_: instance, node, pod

**Tenant**:
A customer served within a multi-tenant Service. Exists only as an attribute value discovered from telemetry — never registered, never granted to.
_Avoid_: project, client, organization

**API Key**:
The credential proving an export comes from a specific Service; it admits telemetry and reads nothing. Shown in full only once at issue and never restorable, but a Service may hold several at once, so a key can be replaced without a gap in ingest.
_Avoid_: token, secret, ingest key

**Retention Policy**:
How long a Service's telemetry is kept: the server-wide default unless the Service overrides it. Two numbers rather than one — Log Retention and Trace Retention. Not stamped on a Signal when it arrives but applied continuously, so shortening a policy reaches back and takes telemetry that is already stored.
_Avoid_: TTL, expiration, lifetime

**Log Retention**, **Trace Retention**:
The two halves of a Retention Policy, each with its own server default and its own per-Service override. Apart because they are read on different clocks: a log is what a week-old incident is reconstructed from, while a trace answers what was slow a moment ago and is dead weight days later — and a Span, carrying its events and links, is the heavier of the two to keep. Traces therefore default to the shorter of them.
_Avoid_: span retention, trace TTL

**Effective Retention**:
The days that actually govern one Service — its own override where it has one, the server-wide default where it has none, resolved for each of the two clocks on its own. What a purge works from, and what a change has to be judged against: dropping an override shortens retention whenever the default is the smaller of the two.
_Avoid_: resolved retention, actual retention

**Catalog**:
The browsable inventory of Applications and their Services.
_Avoid_: directory, inventory, list

**Deletion**:
The permanent removal of an Application or a Service from the Catalog, together with everything registered under it and everything its Services ever sent. Irreversible and never partial.
_Avoid_: archive, disable, decommission, purge
