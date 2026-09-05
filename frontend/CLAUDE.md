# Frontend

- Compose screens from the shadcn-style kit in `src/components/ui`; a new primitive is a design decision, so it belongs in the kit rather than in a screen.
- **User-facing strings live in the typed catalog** `src/i18n/` (ADR 0024): `useT()` in components, `getMessages()` in plain modules, dates and numbers through `getFormatLocale()`. A new string goes into **every** language (en + cs) — the compiler enforces completeness; translate with AI right in the PR.
- Design tokens: the Tailwind v4 theme in [styles/globals.css](styles/globals.css) (brass, IBM Plex, light + `.dark`). Spacing is gap-based; values, ids and timestamps render mono (`font-mono`, `code`/`time`/`[data-mono]`).
- `src/api/schema.d.ts` is generated from Bugler's OpenAPI document and read through openapi-fetch; change the server contract, not the file.
- `bun run arch` (dependency-cruiser) guards module boundaries the same way the architecture tests do on the backend.
- The kit syncs to the "Bugler Design System" project at claude.ai/design: read [../.design-sync/NOTES.md](../.design-sync/NOTES.md) first, and run `/design-sync` after changing the kit, the tokens, or the previews.
