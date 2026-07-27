# Context Map

## Contexts

- [Ingestion](./src/Bugler.Ingestion/CONTEXT.md) — the write path: receives OTLP export requests, authenticates their source, stores telemetry, and purges it when retention expires
- [Exploration](./src/Bugler.Exploration/CONTEXT.md) — the read path: searching, viewing, and correlating stored logs and traces
- [Registry](./src/Bugler.Registry/CONTEXT.md) — the telemetry topology: applications, instances, API keys, retention policies
- [Access](./src/Bugler.Access/CONTEXT.md) — human identity: users, sessions, admin role, per-application read grants

## Relationships

- **Ingestion → Registry**: Ingestion asks Registry to validate an API key (key → InstanceId) and reads effective retention per instance when purging. Nothing else crosses this boundary.
- **Exploration → Access**: Exploration asks Access for the set of applications the current user may read; every query is constrained by that set.
- **Exploration → Registry**: Exploration reads the catalog (application and instance names) for display and filter suggestions.
- **Shared identifiers**: `ApplicationId` and `InstanceId` are the only types shared across contexts; no context reads another context's data store.
