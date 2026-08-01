# Context Map

## Contexts

- [Ingestion](./src/Bugler.Ingestion/CONTEXT.md) — the write path: receives OTLP export requests, authenticates their source, stores telemetry, and purges it when retention expires
- [Exploration](./src/Bugler.Exploration/CONTEXT.md) — the read path: searching, viewing, and correlating stored logs and traces
- [Alerting](./src/Bugler.Alerting/CONTEXT.md) — the unattended watch: detects when a Service starts logging trouble and notifies subscribers by mail and Google Chat
- [Registry](./src/Bugler.Registry/CONTEXT.md) — the telemetry topology: applications, services, API keys, retention policies
- [Access](./src/Bugler.Access/CONTEXT.md) — human identity: users, sessions, admin role, per-application read grants

`Bugler.Mail` is not among them: it is a transport every context may lean on, like SharedKernel. It carries messages and never learns what they mean (ADR 0011).

## Relationships

- **Ingestion → Registry**: Ingestion asks Registry to validate an API key (key → ServiceId) and reads effective retention per service when purging. Nothing else crosses this boundary.
- **Exploration → Access**: Exploration asks Access for the set of applications the current user may read; every query is constrained by that set.
- **Exploration → Registry**: Exploration reads the catalog (application and service names, and the facets a Source Filter offers) for display and filter suggestions.
- **Alerting → Registry**: Alerting reads the catalog to name the Service an Episode belongs to and hangs its detection settings on Applications and Services.
- **Alerting → Access**: at the moment a mail leaves, Alerting asks Access whether the subscribed User is active and may still read the Application — and for the account's address.
- **Registry → Ingestion, Registry → Access, Registry → Alerting**: on Deletion, Registry publishes `ServicesDeleted` and `ApplicationDeleted` through its outbox; Ingestion erases the Signals of the deleted Services, Access revokes the grants pointing at the deleted Application, and Alerting drops the Episodes, Subscriptions, and settings pointing at what was deleted. Registry does not know who listens (ADR 0008).
- **Access → Alerting**: on a User's Deletion, Access publishes `UserDeleted` through its own outbox; Alerting drops the Subscriptions standing in their name and lapses the Deliveries still owed to them.
- **Alerting → Mail**: Alerting sends through `IMailSender` and waits for the outcome, because a Delivery has to record whether the message left and pursue it again if it did not.
- **Shared identifiers**: `ApplicationId`, `ServiceId` and the integration event contracts are the only types shared across contexts.
- **Telemetry storage is shared for reading**: Ingestion alone writes and migrates the `telemetry` schema; Exploration reads it directly (ADR 0009) and Alerting polls it for detection (ADR 0010) — neither ever writes. Every other context keeps its store to itself.
- **Exploration and Alerting stay unrelated even on the Dashboard**: the per-service board reads one aggregate from each and joins them by `ServiceId` in the browser (ADR 0013); no server-side endpoint ever answers for both.
