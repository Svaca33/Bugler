---
status: accepted
---

# The machine hand is a lease, and the verdict stays human

An autonomous agent that debugs from Episodes could read everything and say nothing: it could not
mark that it took a kind of trouble on, two agents could not see each other's work, and the fix it
opened a PR for left no trace on the Episode it came from. This ADR opens a deliberately narrow
write path — the **machine hand**: claim, note, Solved Proposal, Resignation — behind a Machine
Delegation grade stamped at issue (ADR 0029 as revised), while the one thing that must stay human
stays human: **Solved remains a person's verdict**. The machine may claim, annotate and *propose*;
a person confirms, rejects, displaces or sweeps aside.

Three decisions carry the design:

**The claim holds the Episode open, exactly as an Acknowledgement does.** An agent's work takes
longer than a Quiet Window, and an Episode that quiets under the agent would spawn a sibling on the
next match — two Episodes for one investigation, and the agent's marks on the wrong one. The claim
is therefore the same lifecycle hold a human Acknowledgement exerts, and for the same reason:
somebody is on it.

**The hold is a lease, never a possession.** A crashed agent must not leave a zombie Episode, so
the claim wilts unless a machine write renews it, and the sweep that measures Quiet Windows lapses
the expired claims first — each with its Journal line, because no mark may vanish without one. The
human hand always wins at any moment: an Acknowledgement displaces the claim, anyone may withdraw
it, and the machine never touches a human-Acknowledged Episode.

**Machine statements age visibly instead of invalidating themselves.** A Solved Proposal shows the
matches that arrived since it was laid — 0 is persuasive, 400 is a rejection waiting to happen —
because a merged PR takes time to deploy and only a person can weigh that. A proposal or a
Resignation overtaken by a newer Episode of its kind stays readable as history and loses its claim
on anyone's attention. Nothing machine-made ever expires a human decision, and nothing human-made
is ever auto-answered by a machine.

The **Resignation** is the machine hand's second finding, and the asymmetric one: not "I fixed it"
but "this is not one I can fix" — a certificate that expired, a disk that filled. It is a statement
about the machine itself, never an assignment to anyone; it ends the claim (there is nothing left
to hold), bars further Machine Claims until a person sweeps it aside, and — alone among the machine
marks — is delivered to the Episode's audience, because a proposal's PR notifies the code side by
itself while a Resignation has nobody to notify but the people the trouble already concerns.

## Considered Options

- **Let the machine Acknowledge and Solve like a person.** One mark set, no new concepts — and the
  verdict quietly stops being human: an agent that can Solve will Solve, and "the cause was fixed"
  becomes a claim nobody stands behind.
- **A claim that never expires.** Simpler, but a crashed agent then holds an Episode open forever,
  which is exactly the zombie the Quiet Window exists to prevent.
- **Auto-invalidate proposals on new matches.** Honest-looking, but wrong on the common case: a
  merged fix keeps matching until it deploys, and a proposal that dies in that window teaches
  agents to propose late or not at all.
- **A "needs human" flag that assigns the Episode to someone.** The machine does not know who —
  and must not pick. The Resignation states the machine's own limit and stops there; who acts next
  is a person's conclusion, drawn from a message that names the trouble, not an assignee.

## Consequences

- Detection, closing and delivery all learn one new fact — "machine-claimed" — and nothing else
  about agents: Bugler still runs no agent, holds no repo credential, and calls no model here.
- Every machine mark is attributed to the delegation and through it to its User; the Journal's
  sentence grows from "every human hand" to "every hand, and whether it was flesh or machine".
- The lease default (24 h) is an Application-level setting beside sensitivity and the quiet
  window, because how long an agent's overnight run may hold an Episode is the same kind of
  operational fact.
- A second message kind exists (the Resignation's), and it reuses the Alert's audience, channels
  and delivery machinery — no new subscription concept.
- Claim exclusivity is enforced among machines only. Two humans could always contend for a mark;
  two machines cannot, because retrying agents do not de-escalate the way people do.
