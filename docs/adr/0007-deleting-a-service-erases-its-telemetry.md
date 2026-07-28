---
status: accepted
---

# Deleting a Service erases everything it sent

A Service can be deleted from the Catalog, and doing so permanently erases every Signal it ever sent, along with its API keys; deleting an Application does the same for every Service registered under it and drops the read grants pointing at it. Deletion is immediate and irreversible — there is no tombstone, no grace period and no undo, because a self-hosted Bugler is run by the person whose data it is, and a delete that leaves the data lying around is a delete that did not happen. Erasure is bounded by the sender: only the deleted Service's own Signals go, never another Service's.

## Considered Options

- **Soft delete with a grace period** — recoverable, but every read path (Catalog, Exploration, ingest authentication, retention purge) would have to filter tombstones, and the facet uniqueness index would have to tolerate a deleted twin of a live Service. A large permanent cost across four contexts to buy an undo nobody asked for.
- **Two-step deactivate, then delete** — makes the destructive step deliberate, at the price of a second lifecycle state on the Service aggregate and a second set of endpoints and screens. The typed confirmation buys the same deliberateness for none of that.
- **Erasing whole Traces the Service took part in** — leaves no broken waterfalls behind, but deleting one Service would then destroy another Service's spans, possibly under another Application. Nobody asked for that, and it is not recoverable.

## Consequences

- A Trace that crossed Services survives the deletion of one of them with a hole in it: the remaining spans re-root themselves at depth 0 in the waterfall, which is what it already does for a span whose parent was never received.
- The typed confirmation — the Application name, or a Service's `namespace/environment/name` — is the only guard. It exists because the action cannot be walked back, not because the API is dangerous to call.
- Deleting a Service with live senders needs no pre-check: its API keys go with it, so the next Export Request is simply rejected as unauthenticated.
- Bugler records no audit of who deleted what. An audit trail worth having covers every admin action with an actor, a time and an origin; smuggling one in through deletion alone would be worse than not having it.
- The registration is gone before the Signals are, so the window between the two is one where Exploration cannot reach the data anyway: every query resolves its Services through the Catalog first (see ADR 0008 for how the erasure is guaranteed to follow).
