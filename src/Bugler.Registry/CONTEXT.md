# Registry

The source of truth for what sends telemetry into Bugler: the catalog of applications and their client deployments, the API keys that authenticate them, and how long their telemetry is kept.

## Language

**Application**:
A product whose telemetry Bugler collects (e.g. an e-shop, a CRM). The unit at which users are granted read access.
_Avoid_: project, app, system, product

**Instance**:
A single client deployment of an Application; belongs to exactly one Application. Owns its API Key and its retention.
_Avoid_: deployment, environment, installation

**Tenant**:
A customer served within a multi-tenant Instance. Exists only as an attribute value discovered from telemetry — never registered, never granted to.
_Avoid_: project, client, organization

**Service**:
A process within an Instance as it reports itself via OTel `service.name` (e.g. web, worker). Self-declared, not registered.
_Avoid_: component, module

**API Key**:
The credential proving an export comes from a specific Instance. Shown in full only once at issue; revocable but never restorable — a lost or revoked key is replaced by issuing a new one.
_Avoid_: token, secret, ingest key

**Retention Policy**:
How long an Instance's telemetry is kept: the server-wide default unless the Instance overrides it.
_Avoid_: TTL, expiration, lifetime

**Catalog**:
The browsable inventory of Applications and their Instances.
_Avoid_: directory, inventory, list
