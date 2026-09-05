# Backend

Bounded contexts, one per `Bugler.<Context>` folder — start at [CONTEXT-MAP.md](../CONTEXT-MAP.md), which also places `Bugler.SharedKernel`, the `Bugler.Mail` and `Bugler.Ai` transports, and `Bugler.Host`, the composition root that owns deployment topology.

- Each context keeps its own EF `DbContext`, postgres schema, and snake_case naming. The boundaries are enforced by `tests/Bugler.ArchitectureTests`.
- **Ports are surfaces**: one Kestrel listener serves exactly one `Surface`, and its name in appsettings must match one — see [Bugler.Host/ListenerSurfaces.cs](Bugler.Host/ListenerSurfaces.cs). Modules stay port-agnostic. The UI and the REST API deliberately share the app surface, which is what lets cookie auth work without CORS.
- Server sentences live in each module's `…Messages` catalog (ADR 0024): `IRequestLanguage` decides a refusal's language, the recipient's or the server's decides a mail's or a chat post's. Machine-facing text — logs, `/health`, OTLP answers, severity band names — stays English and out of the catalogs.
- `Server:PublicBaseUrl` is how this Bugler is reachable from outside, and because Bugler sees only plain HTTP behind the proxy that terminates TLS, it is also the server's sole statement about whether TLS stands in front of it: an `https` address there is what mints the Session cookie `Secure` and host-locked (ADR 0019).
