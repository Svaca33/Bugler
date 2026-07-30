---
status: accepted
---

# Mail leaves through a shared transport, not through a context

Sending mail lives in `Bugler.Mail`, a transport every context may reference — like SharedKernel, and like it, not a bounded context. It offers two seams: `IMailSender`, which awaits the SMTP server's answer and throws when it does not get one, and `IMailQueue`, which takes the message into a bounded in-memory queue and lets a background loop send it. What a message *says* is composed by whoever sends it; the transport never learns what it means. SMTP moves to a `Mail` section in configuration, and `PublicBaseUrl` — a fact about the server rather than about alerting — moves to a `Server` section that any module may bind.

The prompt was Access needing to mail a password-reset link. SMTP sat in Alerting because Alerting was the first to need it, but the architecture tests forbid Access from depending on Alerting at all, and rightly: the two contexts share no domain.

## Considered Options

- **A port in each context, one SMTP adapter in Host.** No shared node, and defensible — the composition root already owns deployment topology, which is arguably what an SMTP server is. Rejected for duplicating the interface and the options binding in exchange for avoiding a project that is thirty lines of transport.
- **Access publishes an integration event; Alerting sends the mail.** The outbox and the Access → Alerting direction already exist (`UserDeleted`), and it would buy durable retry for free. Rejected on two counts: the reset link is a secret, and an outbox row is a durable plaintext copy of it; and the words of a password-reset mail are Access's language, not the unattended watch's.
- **A second SMTP configuration inside Access.** The operator would configure one mail server twice.
- **Moving `Delivery` — the durable queue with its retries and time-to-live — into the shared transport as well.** Rejected: `Delivery` knows that an All Clear must not overtake its Alert, resolves recipients through Access, and hangs off an `EpisodeId`. Stripped of all that it would be a generic queue nobody asked for, and the shared node would have acquired a table, a lifecycle, and thereby a domain.

## Consequences

- The configuration keys changed: `Alerting:Smtp:*` → `Mail:Smtp:*` and `Alerting:PublicBaseUrl` → `Server:PublicBaseUrl`. An existing deployment keeps running with mail silently disabled until its environment is rewritten.
- `AlertingOptions` is bound twice — once from `Alerting`, once from `Server` — so that `PublicBaseUrl` reaches it without inventing a type shared between contexts. Whoever else needs the value binds the same key the same way.
- A send now has a deadline (`Mail:SendTimeoutSeconds`, 10 s) covering connect, authenticate and send together. Alerting never needed one, sending from a loop nobody waits on; a request handing over a message does.
- Queued mail is **not** durable: nothing survives a restart and a message that fails its retries is logged and dropped. That is a deliberate ceiling — a sender whose message must outlive the process keeps its own record and uses `IMailSender`, as Alerting does.
- The "SMTP is not configured" warning is said once, by the transport, for every context that would have sent something.
- `docker compose` gains a mailpit: mail now has somewhere to go on a developer's machine, which is the first time the alerting mails could be seen at all.
