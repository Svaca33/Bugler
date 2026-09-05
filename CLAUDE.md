# CLAUDE.md

## Commands worth knowing

```bash
cd e2e && bun run test    # Playwright, and it needs `docker compose up -d postgres mailpit` first
dotnet run --project tools/Bugler.SampleSource -- --api-key blgr_…   # sample telemetry into a running Bugler
git tag -a v0.20.0 -m "0.20.0" && git push origin v0.20.0   # THE release: CI builds, pushes to ghcr.io/svaca33/bugler and opens the GitHub Release
```

Everything else is where you would look for it: `dotnet build` / `dotnet test` on `Bugler.slnx`, and the `bun` scripts in `frontend/package.json`.

## Where the rest is written down

- [CONTEXT-MAP.md](CONTEXT-MAP.md) — the bounded contexts and every relationship between them. [docs/agents/domain.md](docs/agents/domain.md) says how to read the domain docs and where each context's `CONTEXT.md` and ADRs sit.
- [CONTRIBUTING.md](CONTRIBUTING.md) — the conventions the build enforces, and the commit-message style this history is written in.
- [src/CLAUDE.md](src/CLAUDE.md) and [frontend/CLAUDE.md](frontend/CLAUDE.md) — the conventions of each side.
- Issues live in GitHub Issues on `Svaca33/Bugler`, driven by `gh` — [docs/agents/issue-tracker.md](docs/agents/issue-tracker.md), with the triage roles in [docs/agents/triage-labels.md](docs/agents/triage-labels.md).

## Rules

- Repo docs, comments, and commit messages are English — talk to the user in Czech.
- **Library work goes through Context7 MCP.** The first time a session touches a library or framework, call `resolve-library-id` and `query-docs` before writing code — and again whenever you reach for an API the repo does not already use, change its configuration, depend on version-specific behaviour, or debug it. Copying a pattern that demonstrably already exists in the repo is the only exception; if Context7 does not know the library, say so in the reply. A `PreToolUse` hook ([scripts/context7-guard.ps1](scripts/context7-guard.ps1)) denies the first library-facing edit of a session once, then stands down.
- **Ask which kind of version a commit is, every time.** Before _any_ `git commit` — whether the user asked for it or you offered — ask whether the change is **patch / minor / major**, and recommend one with a reason. The reason it is easy to miss: `<Version>` in [Directory.Build.props](Directory.Build.props) is `VersionPrefix` + commit count since `VersionPatchBase`, so **committing already ships a patch bump** and a change that deserved a new release line quietly becomes one more patch. Major and minor are made by [scripts/bump-version.ps1](scripts/bump-version.ps1) alone, and it **refuses a dirty tree**, so the bump is always a commit of its own: bump first and let the work land inside the new line, or commit the work as the tail of the old line and bump after.
- **Finish a change with a redeploy.** When the work touched `src/`, `frontend/`, `Dockerfile` or `docker-compose.yml`, run [scripts/redeploy.ps1](scripts/redeploy.ps1) (or `/redeploy`) _before_ declaring it done: the image bakes the frontend, so manual testing has to happen on the rebuilt container at http://localhost:8080, not the hot-reloaded sources on :3000. postgres stays up. Commit only after the user has clicked through it. Changes confined to `tests/`, `e2e/` or docs need no redeploy.
