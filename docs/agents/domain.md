# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

Bugler is a **multi-context** repo: a modular monolith of bounded contexts, each with its own `CONTEXT.md` and, where it has made decisions of its own, its own ADR folder.

## Before exploring, read these

- **`CONTEXT-MAP.md`** at the repo root: the map of contexts and the relationships between them. Start here.
- **`src/<context>/CONTEXT.md`**: read the one for each context relevant to the topic. They exist for `Bugler.Access`, `Bugler.Alerting`, `Bugler.Exploration`, `Bugler.Ingestion`, and `Bugler.Registry`.
- **`docs/adr/`**: system-wide decisions. Read the ADRs that touch the area you're about to work in.
- **`src/<context>/docs/adr/`**: context-scoped decisions. Present for `Bugler.Access`, `Bugler.Alerting`, and `Bugler.Exploration`; a context may gain one at any time, so look rather than assume.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

```
/
├── CONTEXT-MAP.md
├── docs/adr/                          ← system-wide decisions
└── src/
    ├── Bugler.Access/
    │   ├── CONTEXT.md
    │   └── docs/adr/                  ← context-specific decisions
    ├── Bugler.Alerting/
    │   ├── CONTEXT.md
    │   └── docs/adr/
    ├── Bugler.Exploration/
    │   ├── CONTEXT.md
    │   └── docs/adr/
    ├── Bugler.Ingestion/
    │   └── CONTEXT.md
    ├── Bugler.Registry/
    │   └── CONTEXT.md
    ├── Bugler.SharedKernel/           ← shared ids, primitives, integration event contracts
    ├── Bugler.Mail/                   ← shared transport, not a context (ADR 0011)
    └── Bugler.Host/                   ← composition root
```

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in the owning context's `CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal: either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0007 (deleting a service erases its telemetry), but worth reopening because…_
