# Contributing

Bugler is written and maintained by one person in their own time. That single fact explains every
rule below, so it is worth stating before the rules: **issues are read, nothing is promised.** There
is no service level, no guarantee that a request will be built, and no offence taken if you fork it
instead.

## What is welcome, and how

**Bug reports** are the most useful thing you can send. Open an issue and say which version you run,
how Bugler is deployed, what you expected and what happened. The template asks for exactly that.

**Questions** about running Bugler belong in an issue too. They are not a nuisance — a question
usually means the documentation failed, and that is worth fixing.

**Pull requests** come in two kinds, and the difference matters:

- **Send it straight away** for a typo, a documentation fix, or a bug fix that is contained and
  obviously correct. No permission needed.
- **Open an issue first** for a new feature or any change in behaviour, and wait for a reply. Bugler
  has explicit boundaries between its contexts and a written reason for most of its shape (see
  [ADRs](docs/adr)); a change that does not know about them costs more to review than to write.
  **A pull request that changes behaviour without a prior agreement may be closed unmerged**, and it
  is nothing personal — it is the only way one person can keep the door open at all.

By opening a pull request you license your contribution under the Apache License 2.0, the same terms
as the project (see section 5 of the [LICENSE](LICENSE)). There is no separate agreement to sign.

## Security

Do not open an issue for a vulnerability. [SECURITY.md](SECURITY.md) says where it goes instead.

## Working on the code

```bash
dotnet build Bugler.slnx
dotnet test Bugler.slnx                       # unit + architecture + integration (needs Docker)
cd frontend && bun install && bun test && bun run typecheck && bun run arch
docker compose up -d --build bugler           # the whole stack on :8080, as it ships
```

`docker compose` also raises a **mailpit** on http://localhost:8025, which swallows every message
Bugler sends, so alerting and password resets can be exercised without a real inbox.

More detail on the layout lives in [CONTEXT-MAP.md](CONTEXT-MAP.md) and in each module's
`CONTEXT.md`.

### What the build enforces

Some conventions are not style opinions — they fail the build, so knowing them saves a round trip:

- **Context boundaries.** `tests/Bugler.ArchitectureTests` fails any dependency that crosses a
  bounded context outside its `Contracts` namespace; `bun run arch` (dependency-cruiser) does the
  same on the frontend.
- **No hard-coded user-facing strings.** UI text goes through the typed catalog in
  `frontend/src/i18n/`, server sentences through each module's `…Messages` catalog. A string must be
  added in **both** languages (`en` and `cs`) — the compilers check completeness. Machine-facing
  text (logs, `/health`, OTLP answers) stays English and stays out of the catalogs.
- **The UI kit is composed, not extended.** Build from the primitives in
  `frontend/src/components/ui`; a new primitive is a design decision, not an implementation detail.
- **No `X-` prefixed headers** ([RFC 6648](https://www.rfc-editor.org/rfc/rfc6648)). API keys travel
  as `Authorization: Bearer`.

### Decisions are written down

Anything that constrains the future — a storage choice, a protocol boundary, a security posture —
is an [ADR](docs/adr). If your change contradicts one, the ADR is the thing to argue with, and
superseding one is a legitimate outcome. If your change *establishes* such a constraint, it needs an
ADR of its own in the same pull request.

### Commit messages

The history is documentation here, and it reads like it. A subject line says what the change makes
true, in the imperative and without a type prefix — *Serve the log list from a timestamp-leading
index*, not *feat(exploration): add index*. The body explains why, what was considered, and what was
deliberately left alone. `git log` is the best introduction to the style, and matching it is
appreciated rather than required.

### Versions

`Directory.Build.props` holds the version. Patch is derived from the commit count, so it moves on
its own; major and minor are raised only by `scripts/bump-version.ps1`. **A pull request should not
touch either.**
