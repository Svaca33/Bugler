---
status: accepted
---

# The Dashboard composes per-context reads in the client

The Dashboard (`/dashboard`) shows one tile per visible Service: its log volume in the chosen
window, its Episodes, and whether the caller is mailed about it. That picture spans three
contexts — volume belongs to Exploration, Episodes to Alerting, identity to Registry via the
catalog — and [CONTEXT-MAP.md](../../CONTEXT-MAP.md) gives Exploration and Alerting no
relationship with each other. Rather than invent one, the server exposes two per-context
aggregates — `GET /api/logs/volume/by-service` (Exploration) and
`GET /api/alerting/episodes/by-service` (Alerting) — and **the frontend joins them by
`serviceId`**, with the catalog as the spine (`frontend/src/features/overview/serviceOverview.ts`).

The alternative was one joined dashboard endpoint. It was rejected because any single handler
answering "volume and episodes per service" has to reference both contexts, which
`tests/Bugler.ArchitectureTests` rightly forbids; hosting it in a fourth place would create a
context whose only job is to know everyone else's read models. The join itself is trivial — a
dictionary lookup per Service — and the browser is the one place that already holds all three
answers for free.

## Consequences

- The board costs four HTTP requests (catalog, volume, episodes, subscriptions), all polled at
  30 s. That is the price of the seam, accepted deliberately: N-per-service requests were the
  thing to avoid, and four flat requests for the whole board is not a fan-out.
- Each aggregate can fail alone, and the board renders what still stands — a 503 from volume
  must not take the Episode counts down with it. The client owns that judgment.
- The two responses may be mutually inconsistent for one poll beat (an Episode on a Service the
  catalog no longer names, volume for a Service with no Episode row). The join treats the
  catalog as authoritative and drops or zero-fills the rest.
- Derived per-service status (`open` / `recovered` / `calm`) is frontend vocabulary, defined and
  tested in `serviceOverview.ts` — deliberately not an Alerting term, and named apart from
  Alerting's "Quieted" so one word never means two things.
