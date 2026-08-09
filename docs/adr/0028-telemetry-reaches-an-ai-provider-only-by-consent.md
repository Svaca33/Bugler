---
status: accepted
---

# Telemetry reaches an AI provider only by the Application's consent

Telemetry is production data, and a prompt is a disclosure: log bodies, an opening record's
attributes, release versions — shown to whatever the server's AI settings point at, be it a
vendor's API or the operator's own machine. That disclosure is governed per Application by a
fact Registry holds beside its other governance: **AI Consent**, off by default for new and
existing Applications alike, turned on only by an Admin on the Application's detail, where the
switch says in plain words what leaves and where it goes. It is read at the moment the data
would leave — never earlier, never cached across that moment — so withdrawing consent stops the
very next disclosure. Two gates stand in a row: the server has AI configured (ADR 0027), and
the Application has consented; either alone sends nothing.

## Considered Options

- **A global switch.** One operator decision covering every Application's data — but the
  Application is Bugler's unit of ownership and of read access, and the operator of the server
  is not always the owner of the product whose logs it holds.
- **Consent per Service.** Finer than the decision being made: consent is governance of a
  product's data, not of one of its processes. A Service-level override can come later if
  anyone asks; the reverse migration could not.
- **Opt-out, on by default.** Every AI feature would work out of the box — and production data
  would flow to a third party because nobody read the release notes. Unacceptable.
- **Consent held by Alerting, the first consumer.** Traps a data-governance fact inside one
  feature; the next consumer (Exploration, embeddings) would need the same answer and could not
  reach it.

## Consequences

- Registry's Application carries the flag, an Admin-only endpoint flips it, and the contracts
  expose it for other contexts to read at their moment of disclosure.
- Every AI feature must degrade silently where consent is absent — for the Reading: no pending
  row, and the Alert leaves at once.
- The consent screen's description and the prompts' actual content are one promise in two
  places: whoever changes what a prompt contains owes the consent text the same change.
- Solved stays a human verdict everywhere; nothing generated ever acts on an Episode. Consent
  covers showing data to a model — it does not delegate any decision to one.
