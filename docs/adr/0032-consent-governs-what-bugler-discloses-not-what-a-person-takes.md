---
status: accepted
---

# Consent governs what Bugler discloses, not what a person takes

ADR 0028 gates telemetry reaching the server's configured AI provider on the Application's AI
Consent, and MCP looks like the same act performed again. It is not. There, Bugler composes a
prompt on its own initiative and sends it, unattended, to a provider its operator configured. Here,
a person exercises their own reading, at their own keyboard, through a client whose provider Bugler
never learns of. The actor is different, and so is what Bugler can honestly promise. **A Machine Delegation
is therefore not gated by AI Consent**, and Registry is never asked about it.

Extending consent to cover it would rewrite the term from *shown to the server's provider* into
*shown to any model anywhere* — a sentence Bugler cannot keep, because the same person may select
Log Records in the UI and paste them wherever they like, and consent never governed that. A rule
that cannot be enforced while behaving as though it can is worse than no rule: it tells an Admin
they decided something they did not.

What the argument concedes is real: MCP differs from copy-paste in scale and in unattendedness — an
agent pulls hundreds of records, repeatedly, with nobody reading them. The answer is not to borrow a
gate built for a different actor, but to hand the operator the ones that fit this one, and they
exist: the Admin's server switch and the port they need not route (ADR 0030), a Machine Delegation narrowed
to one Application and expiring, and the Admin's sight of every Machine Delegation issued with the power to
revoke any (ADR 0029). And what leaves is bounded in kind as well as in size — a Reading travels
labelled as a machine's reading of evidence rather than as evidence, so nothing a model wrote
re-enters another model wearing the clothes of fact.

## Consequences

- AI Consent's definition does not change and Registry gains nothing. The two gates of ADR 0028
  still stand for what they always governed: Bugler's own disclosures.
- **No audit log, here or anywhere.** Bugler records who read what neither through MCP nor through
  the UI. This follows from the same reasoning: reading through an agent and reading through a
  browser are one act by one person, so the day either is audited both must be, under a decision
  about server-activity records that Bugler has not yet had to make — retention today bounds
  telemetry, not the server's own history.
- Whoever changes what a tool returns owes nothing to the consent text (which is ADR 0028's rule
  for prompts) — but owes the same honesty to the switch's own description, which is where an
  operator learns what this door lets out.
