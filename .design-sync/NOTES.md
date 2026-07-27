# design-sync notes — Bugler

- This is an app repo, not a packaged DS. The design surface is `frontend/src/components/ui/` (shadcn-style primitives). No library build exists — the converter runs in synth-entry mode; `--entry` must point at a non-existent path under `frontend/` (e.g. `frontend/dist-ds/index.js`) purely so PKG_DIR resolves to `frontend/`.
- `srcDir` is scoped to `src/components` deliberately: default `src/` would `export *` the whole app (routes, pages, API client) into the bundle.
- Compound sub-parts (CardHeader, SelectTrigger, …) are bundle exports but intentionally NOT separate cards — the 6 cards come from `componentSrcMap` pins; sub-parts are documented in the parent's doc.
- CSS: Tailwind v4 source CSS isn't browser-consumable. `frontend/ds-css/build.ts` (bun + the repo's own bun-plugin-tailwind) compiles `frontend/ds-css/entry.css` → `frontend/.ds-css/entry.css` (= `cfg.cssEntry`). The entry `@source`-scans `frontend/src` AND `.design-sync/previews`, so **re-run `buildCmd` after authoring/editing previews** — otherwise preview utility classes are missing from the compiled CSS.
- Tokens: standard shadcn neutral theme in `frontend/styles/globals.css` (oklch, light + `.dark`). No custom fonts — system font stack; `[FONT_MISSING]` is not expected.
- Playwright: machine cache has chromium-1234 (`%LOCALAPPDATA%/ms-playwright`); repo's `e2e/` pins `@playwright/test ^1.62.0`.
- `FilterSelect` (frontend/src/features/explore) is app-level glue over Select — excluded from the DS by user scope decision (2026-07-27).
- Converter deps in `.ds-sync/`: pin `typescript@5` — `typescript@latest` resolves to the v7 (Go) rewrite whose package no longer exports the old compiler API, which silently skips validate's `.d.ts` parse check. `playwright@1.62.0` matches the cached chromium-1234 and the repo's e2e pin.
- Windows: `package-build.mjs` can hit a transient `EPERM: rm ds-bundle` (lingering chromium/AV handle). Just retry — second run succeeds.
- `Select` uses `cfg.overrides.Select = {cardMode: "single", primaryStory: "OpenWithGroups"}` — the open popover portals outside grid cells (`[GRID_OVERFLOW]` escape case). The other stories (Default, FilterBar) remain addressable via `?story=` and in `.review.html`, just not on the card face.
- `frontend/ds-css/entry.css` carries an `@source inline()` safelist of semantic utilities no app source uses as a bare class (`text-foreground` etc.). **Keep it in sync with `.design-sync/conventions.md`** — the conventions validation greps compiled CSS for every named class.
- `--radius-sm/md/lg/xl` are `@theme inline` — compile-time only, never runtime CSS variables. Only `--radius` exists at runtime. Conventions doc reflects this; don't "fix" it back.

## Known render warns

(none — all warns from this campaign were fixed, not triaged-as-legitimate)

## Re-sync risks

- **Compiled-CSS snapshot**: `_ds_bundle.css` only contains utilities scanned from `frontend/src` + `.design-sync/previews` at `buildCmd` time. New previews or app code using new utilities need `buildCmd` re-run before the converter, or the classes silently miss. When in doubt, always re-run `buildCmd`.
- **Conventions drift**: `.design-sync/conventions.md` enumerates classes/tokens — re-run its validation (grep against fresh `_ds_bundle.css` + component dirs) on every re-sync; app refactors can drop utilities from the scan.
- **shadcn upstream refresh**: `frontend/src/components/ui/*.tsx` are vendored shadcn code — a regeneration (e.g. `npx shadcn@latest`) changes render hashes and correctly triggers re-verify of everything.
- **Toolchain assumptions**: bun 1.3.x + bun-plugin-tailwind (repo dep) build the CSS; node 24 + npm-installed esbuild/ts-morph/typescript@5/playwright@1.62.0 (`.ds-sync/`, regenerated per clone) run the converter. No network-fetched assets; no fonts shipped (system stack).
- **Grades**: verification state lives in the uploaded `_ds_sync.json` (project) and gitignored `.cache/` — a fresh clone re-verifies only what the anchor can't vouch for.
