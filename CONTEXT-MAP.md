# Context Map

## Contexts

- [Ingestion](./src/Bugler.Ingestion/CONTEXT.md) — the write path: receives OTLP export requests, authenticates their source, stores telemetry, and purges it when retention expires
- [Exploration](./src/Bugler.Exploration/CONTEXT.md) — the read path: searching, viewing, and correlating stored logs and traces
- [Alerting](./src/Bugler.Alerting/CONTEXT.md) — the unattended watch: detects when a Service starts logging trouble and notifies subscribers by mail and Google Chat
- [Registry](./src/Bugler.Registry/CONTEXT.md) — the telemetry topology: applications, services, API keys, retention policies
- [Access](./src/Bugler.Access/CONTEXT.md) — human identity: users, sessions, admin role, per-application read grants

## Relationships

- **Ingestion → Registry**: Ingestion asks Registry to validate an API key (key → ServiceId) and reads effective retention per service when purging. Nothing else crosses this boundary.
- **Exploration → Access**: Exploration asks Access for the set of applications the current user may read; every query is constrained by that set.
- **Exploration → Registry**: Exploration reads the catalog (application and service names, and the facets a Source Filter offers) for display and filter suggestions.
- **Alerting → Registry**: Alerting reads the catalog to name the Service an Episode belongs to and hangs its detection settings on Applications and Services.
- **Alerting → Access**: at the moment a mail leaves, Alerting asks Access whether the subscribed User is active and may still read the Application — and for the account's address.
- **Registry → Ingestion, Registry → Access, Registry → Alerting**: on Deletion, Registry publishes `ServicesDeleted` and `ApplicationDeleted` through its outbox; Ingestion erases the Signals of the deleted Services, Access revokes the grants pointing at the deleted Application, and Alerting drops the Episodes, Subscriptions, and settings pointing at what was deleted. Registry does not know who listens (ADR 0008).
- **Shared identifiers**: `ApplicationId`, `ServiceId` and the integration event contracts are the only types shared across contexts.
- **Telemetry storage is shared for reading**: Ingestion alone writes and migrates the `telemetry` schema; Exploration reads it directly (ADR 0009) and Alerting polls it for detection (ADR 0010) — neither ever writes. Every other context keeps its store to itself.
