# CLAUDE.md

Bugler is a self-hosted observability tool (OTLP logs + traces in, explore UI out): .NET 10 modular monolith + React 19 SPA.

## Commands

```bash
dotnet build Bugler.slnx
dotnet test Bugler.slnx            # unit + architecture + integration (integration needs Docker: Testcontainers postgres)
cd frontend && bun install && bun test && bun run typecheck
cd frontend && bun dev             # UI on :3000, proxies /api + /openapi to :8080
cd e2e && bun run test             # Playwright; needs `docker compose up -d postgres mailpit` first
docker compose up -d --build bugler  # full stack on :8080; image bakes the frontend — rebuild after ANY change
powershell -File scripts/redeploy.ps1  # finish a change: build+typecheck gate → stop dev servers → rebuild the bugler container → wait for /health
powershell -File scripts/publish-image.ps1 -Repository svaca33/bugler  # build + tag as major.minor.<commits since that major.minor began>; refuses a dirty tree; -Push to publish
powershell -File scripts/bump-version.ps1 -Minor    # start a new release line (-Major too); edits Directory.Build.props only
dotnet run --project tools/Bugler.SampleSource -- --api-key blgr_…  # stream sample telemetry into a running Bugler (tools/Bugler.SampleSource/README.md)
```

## Architecture

Modular monolith of bounded contexts — see [CONTEXT-MAP.md](CONTEXT-MAP.md) and each module's `CONTEXT.md`:

- `src/Bugler.Access` — local accounts, cookie sessions, per-app grants; **first user created becomes Admin** (no seeded credentials)
- `src/Bugler.Registry` — applications, services, API keys (`Authorization: Bearer blgr_…`)
- `src/Bugler.Ingestion` — OTLP receivers (gRPC + HTTP), buffered writers, retention purge
- `src/Bugler.Exploration` — read path for the UI (logs, traces, waterfall)
- `src/Bugler.Alerting` — the unattended watch: polls stored logs, opens Episodes, mails subscribers and posts to Google Chat
- `src/Bugler.SharedKernel` — shared ids/primitives and the integration event contracts (no behaviour)
- `src/Bugler.Mail` — shared mail transport, not a context (ADR 0011): `IMailSender` awaits its outcome, `IMailQueue` hands off to a background loop. SMTP under `Mail` in appsettings; unset SMTP disables mail everywhere
- `src/Bugler.Host` — composition root; owns deployment topology

`Server:PublicBaseUrl` (how this Bugler is reachable from outside) is configured once and read by every module that puts a link in a message — and, because Bugler sees only plain HTTP behind the proxy that terminates TLS, it is also the server's sole statement about whether TLS stands in front of it: an https address there is what mints the Session cookie `Secure` and host-locked (ADR 0019).

`docker compose` runs a **mailpit** alongside Bugler: everything Bugler mails is read at http://localhost:8025 instead of being delivered.

Module boundaries are enforced by `tests/Bugler.ArchitectureTests` (backend) and dependency-cruiser (`cd frontend && bun run arch`).

## Rules and conventions

- DDD + SOLID rigor; each context keeps its own EF `DbContext`, postgres schema, and snake_case naming.
- **Ports are surfaces** (enforced in [src/Bugler.Host/ListenerSurfaces.cs](src/Bugler.Host/ListenerSurfaces.cs)): 8080 = app (UI + REST + OpenAPI), 4317 = OTLP/gRPC, 4318 = OTLP/HTTP, `/health` everywhere. Kestrel listener names in appsettings must match a `Surface`; modules stay port-agnostic. UI and REST API intentionally share one origin (cookie auth, no CORS).
- No `X-` prefixed headers (RFC 6648) — API keys travel as `Authorization: Bearer`.
- Repo docs, comments, and commit messages are English — but talk to the user in Czech (chat replies, questions, summaries).
- **Library work goes through Context7 MCP.** The first time a session touches a given library or framework, call `resolve-library-id` and `query-docs` before writing code — and again whenever you reach for an API the repo does not already use, change its configuration, depend on version-specific behaviour, or debug it. Copying a pattern that demonstrably already exists in the repo is the only exception. If Context7 does not know the library or the server is unavailable, say so in the reply instead of guessing. A `PreToolUse` hook ([scripts/context7-guard.ps1](scripts/context7-guard.ps1)) denies the first library-facing edit of a session once, then stands down.
- **Ask which kind of version a commit is, every time.** Before *any* `git commit` — whether the user asked for it or you offered — ask whether the change is **patch / minor / major**, and say which one you recommend and why. Never decide it silently. The reason it is easy to miss: the patch number is derived, not typed. `<Version>` in [Directory.Build.props](Directory.Build.props) is `VersionPrefix` + commit count since `VersionPatchBase`, so **committing already ships a patch bump** and a change that deserved a new release line quietly becomes one more patch. Only major and minor are a decision, and they are made by [scripts/bump-version.ps1](scripts/bump-version.ps1) — which **refuses a dirty tree**, so the bump is always a commit of its own: either bump first and let the work land inside the new line, or commit the work as the tail of the old line and bump after. Nothing but that script may touch `VersionPatchBase`.
- **Finish a change with a redeploy.** When the work touched `src/`, `frontend/`, `Dockerfile` or `docker-compose.yml`, run [scripts/redeploy.ps1](scripts/redeploy.ps1) (or `/redeploy`) *before* declaring it done: it stops the dev servers and rebuilds the `bugler` container, so manual testing happens on http://localhost:8080 — the production build that actually ships, not the hot-reloaded sources on :3000. postgres stays up. Commit only after the user has clicked through it. Changes confined to `tests/`, `e2e/` or docs do not reach the image and need no redeploy.

## Frontend

- React 19 + TanStack Router/Query, shadcn-style kit in `frontend/src/components/ui` (Button, Card, Input, Label, Select, Textarea) — compose these, don't invent new primitives.
- **No hardcoded user-facing strings** (ADR 0024): UI text goes through the typed catalog in `frontend/src/i18n/` (`useT()` in components, `getMessages()` in plain modules; dates/numbers via `getFormatLocale()`), server sentences through each module's `…Messages` catalog (`IRequestLanguage` for refusals, recipient/server language for mail and chat). Adding a string means adding it to **every** language (en + cs) — the compilers enforce completeness; translate with AI right in the PR. Machine-facing text (logs, `/health`, OTLP answers, severity band names) stays English.
- Design tokens: Tailwind v4 theme in [frontend/styles/globals.css](frontend/styles/globals.css) (brass theme, IBM Plex, light + `.dark`). Spacing is gap-based; values/ids/timestamps render mono (`font-mono`, `code`/`time`/`[data-mono]`).
- API client is generated from OpenAPI (`frontend/src/api/schema.d.ts`, openapi-fetch).

## Design sync (claude.ai/design)

The UI kit syncs to the "Bugler Design System" project via the `/design-sync` skill. Config, notes (gotchas + re-sync risks), authored previews, and the conventions header live in [.design-sync/](.design-sync/NOTES.md) — read NOTES.md before touching the sync. After changing the UI kit, tokens, or previews, run `/design-sync` again.
