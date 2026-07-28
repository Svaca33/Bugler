---
status: accepted
---

# A User can be deactivated or deleted, and neither is a step towards the other

A User has two ways out. Deactivation withdraws their access and keeps their Application Grants for a return; Deletion removes the account and its grants for good and frees the e-mail. Either applies to any User at any time — a Deletion needs no Deactivation before it. This is deliberately unlike a Service, which ADR 0007 gave a single irreversible Deletion and no lifecycle state at all: a Service either sends or it does not, and pausing one means nothing, whereas a person who is away for six months is a more ordinary case than a person who is gone for good.

What keeps a server administrable is that **an Admin may neither deactivate nor delete their own account**. The whole `/api/users` surface sits behind the Admin policy, so the last Admin can only be removed by themselves — forbidding that is enough to guarantee that a server holding any Users holds at least one Admin who can still sign in.

## Considered Options

- **Deletion instead of Deactivation**, one way out exactly like a Service. Rejected because the operator would then delete a departing colleague and re-create them on return, losing every grant and reassigning them by hand.
- **Deactivation as a mandatory first step.** Rejected for the reason ADR 0007 already gave for Services, and because an account created with a typo in the e-mail would have to be paused before it could be thrown away.
- **Refusing any operation that would leave no active Admin**, instead of refusing self-removal. States the invariant directly and would let an Admin delete themselves once a successor exists, but it is the more elaborate rule for an outcome the simpler one already reaches.
- **Typing the e-mail back to confirm a Deletion**, as ADR 0007 requires for a Service. Rejected because deleting a User destroys no telemetry; a dialog naming the account is proportionate to what is lost.

## Consequences

- Bugler's typed confirmation now means "irreversible *and* large", not merely "irreversible". A plain dialog is what guards the smaller irreversible actions.
- The self-removal guard stops being sufficient the moment an Admin can be demoted by somebody else. Whoever adds promotion or demotion has to replace it with the invariant it currently only implies.
- Reactivation restores the grants the User had, because Deactivation never touched them. Grants cannot be edited while a User is deactivated.
- Nothing records that a User was deleted, or by whom. Bugler keeps no audit (ADR 0007) and Deletion is not the place to start one.
- Deletion publishes no integration event: no context outside Access stores a `UserId`, and Exploration resolves visibility per request rather than holding it.
- A deleted e-mail is immediately free again, and re-creating the account produces a different User with no grants — a new person, as far as Bugler is concerned.
- Deleting every User would return the server to its unconfigured state, where whoever arrives next becomes Admin through `/api/auth/setup`. The API can never quite get there: the last Admin cannot remove themselves.
