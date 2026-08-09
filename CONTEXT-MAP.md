# Context Map

## Contexts

- [Ingestion](./src/Bugler.Ingestion/CONTEXT.md) — the write path: receives OTLP export requests, authenticates their source, stores telemetry, and purges it when retention expires
- [Exploration](./src/Bugler.Exploration/CONTEXT.md) — the read path: searching, viewing, and correlating stored logs and traces
- [Alerting](./src/Bugler.Alerting/CONTEXT.md) — the unattended watch: detects when a Service starts logging trouble or stops answering that it is alive, and notifies subscribers by mail and Google Chat
- [Registry](./src/Bugler.Registry/CONTEXT.md) — the telemetry topology: applications, services, API keys, retention policies
- [Access](./src/Bugler.Access/CONTEXT.md) — human identity: users, sessions, admin role, per-application read grants

`Bugler.Mail` is not among them: it is a transport every context may lean on, like SharedKernel. It carries messages and never learns what they mean (ADR 0011). `Bugler.Ai` stands on the same ground: it carries a prompt to the configured AI provider and returns the answer, learning nothing about either (ADR 0027) — and unset settings mean AI is off everywhere.

## Relationships

- **Ingestion → Registry**: Ingestion asks Registry to validate an API key (key → ServiceId) and reads effective retention per service — when purging, and when reporting each Service's storage Footprint beside the number the purge works from (ADR 0017). Nothing else crosses this boundary.
- **Exploration → Access**: Exploration asks Access for the set of applications the current user may read; every query is constrained by that set.
- **Exploration → Registry**: Exploration reads the catalog (application and service names, and the facets a Source Filter offers) for display and filter suggestions.
- **Alerting → Registry**: Alerting reads the catalog to name the Service an Episode belongs to and hangs its detection settings on Applications and Services. It also asks, at the moment a Reading's context would leave for the AI provider, whether the Application's AI Consent stands (ADR 0028) — never earlier.
- **Alerting → Access**: at the moment a mail leaves, Alerting asks Access whether the subscribed User is active and may still read the Application — and for the account's address and the Language the mail should speak (ADR 0024).
- **Registry → Ingestion, Registry → Access, Registry → Alerting**: on Deletion, Registry publishes `ServicesDeleted` and `ApplicationDeleted` through its outbox; Ingestion erases the Signals of the deleted Services, Access revokes the grants pointing at the deleted Application, and Alerting drops the Episodes, Subscriptions, and settings pointing at what was deleted. Registry does not know who listens (ADR 0008).
- **Access → Alerting**: on a User's Deletion, Access publishes `UserDeleted` through its own outbox; Alerting drops the Subscriptions standing in their name and lapses the Deliveries still owed to them.
- **Alerting → the watched Services themselves**: the Health Check Watch calls each Service's configured address on the loop's beat (ADR 0008 in Alerting's own log). This is the only outbound connection Bugler ever opens to a Service — everywhere else telemetry is pushed to Bugler, never fetched (ADR 0006). Nothing is read but the status code, and the address is Alerting's own setting; Registry does not learn that Services have addresses.
- **Alerting → Mail**: Alerting sends through `IMailSender` and waits for the outcome, because a Delivery has to record whether the message left and pursue it again if it did not.
- **Host → Mail**: the Host stores the SMTP settings the admin screen edits and hands them to the transport through `ISmtpSettingsSource`; saved settings win over the `Mail:Smtp` configuration section until reset (ADR 0014). Mail itself still owns no data.
- **Host → Ai**: the AI settings live on the same terms — stored by Host, handed to the transport through `IAiSettingsSource`, the saved row winning over the `Ai` configuration section until reset (ADR 0027). Ai owns no data either.
- **Host → everyone who speaks**: the Host stores the server's Language on the same terms as the SMTP settings and answers for it through `IServerLanguage`; each request's answer leaves in the language `IRequestLanguage` resolves from `Accept-Language`, falling back to the server's (ADR 0024). Every context keeps its own sentences in its own typed catalog.
- **Shared identifiers**: `ApplicationId`, `ServiceId`, `Language` and the integration event contracts are the only types shared across contexts.
- **Telemetry storage is shared for reading**: Ingestion alone writes and migrates the `telemetry` schema; Exploration reads it directly (ADR 0009) and Alerting polls it for detection (ADR 0010) and reads it once more — the logs around an opening match, and the Service's last Release — when assembling a Reading's context; neither ever writes. Every other context keeps its store to itself. `telemetry.releases` is written on the same terms and is the one table there that retention does not bound (ADR 0016).
- **Ingestion observes Releases, Exploration renders them**: a change of `service.version` is noticed by the write path as the Export Request arrives and stored as a Release; Exploration serves it to the UI, which lays it over the Volume and beside an Episode. Ingestion does not learn who reads it, and Alerting stays out of the rendering — the Episodes view joins Episodes to Releases in the browser, as ADR 0013 has it; the one place Alerting reads a Release is the context of a Reading.
- **Exploration and Alerting stay unrelated even on the Dashboard**: the per-service board reads one aggregate from each and joins them by `ServiceId` in the browser (ADR 0013); no server-side endpoint ever answers for both.
