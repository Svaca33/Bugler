# CLAUDE.md

Bugler is a self-hosted observability tool (OTLP logs + traces in, explore UI out): .NET 10 modular monolith + React 19 SPA.

## Commands

```bash
dotnet build Bugler.slnx
dotnet test Bugler.slnx            # unit + architecture + integration (integration needs Docker: Testcontainers postgres)
cd frontend && bun install && bun test && bun run typecheck
cd frontend && bun dev             # UI on :3000, proxies /api + /openapi to :8080
cd e2e && bun run test             # Playwright; needs `docker compose up -d postgres` first
docker compose up -d --build bugler  # full stack on :8080; image bakes the frontend — rebuild after ANY change
dotnet run --project tools/Bugler.SampleSource -- --api-key blgr_…  # stream sample telemetry into a running Bugler (tools/Bugler.SampleSource/README.md)
```

## Architecture

Modular monolith of bounded contexts — see [CONTEXT-MAP.md](CONTEXT-MAP.md) and each module's `CONTEXT.md`:

- `src/Bugler.Access` — local accounts, cookie sessions, per-app grants; **first user created becomes Admin** (no seeded credentials)
- `src/Bugler.Registry` — applications, services, API keys (`Authorization: Bearer blgr_…`)
- `src/Bugler.Ingestion` — OTLP receivers (gRPC + HTTP), buffered writers, retention purge
- `src/Bugler.Exploration` — read path for the UI (logs, traces, waterfall)
- `src/Bugler.SharedKernel` — shared ids/primitives and the integration event contracts (no behaviour)
- `src/Bugler.Host` — composition root; owns deployment topology

Module boundaries are enforced by `tests/Bugler.ArchitectureTests` (backend) and dependency-cruiser (`cd frontend && bun run arch`).

## Rules and conventions

- DDD + SOLID rigor; each context keeps its own EF `DbContext`, postgres schema, and snake_case naming.
- **Ports are surfaces** (enforced in [src/Bugler.Host/ListenerSurfaces.cs](src/Bugler.Host/ListenerSurfaces.cs)): 8080 = app (UI + REST + OpenAPI), 4317 = OTLP/gRPC, 4318 = OTLP/HTTP, `/health` everywhere. Kestrel listener names in appsettings must match a `Surface`; modules stay port-agnostic. UI and REST API intentionally share one origin (cookie auth, no CORS).
- No `X-` prefixed headers (RFC 6648) — API keys travel as `Authorization: Bearer`.
- Repo docs, comments, and commit messages are English.

## Frontend

- React 19 + TanStack Router/Query, shadcn-style kit in `frontend/src/components/ui` (Button, Card, Input, Label, Select, Textarea) — compose these, don't invent new primitives.
- Design tokens: Tailwind v4 theme in [frontend/styles/globals.css](frontend/styles/globals.css) (brass theme, IBM Plex, light + `.dark`). Spacing is gap-based; values/ids/timestamps render mono (`font-mono`, `code`/`time`/`[data-mono]`).
- API client is generated from OpenAPI (`frontend/src/api/schema.d.ts`, openapi-fetch).

## Design sync (claude.ai/design)

The UI kit syncs to the "Bugler Design System" project via the `/design-sync` skill. Config, notes (gotchas + re-sync risks), authored previews, and the conventions header live in [.design-sync/](.design-sync/NOTES.md) — read NOTES.md before touching the sync. After changing the UI kit, tokens, or previews, run `/design-sync` again.
