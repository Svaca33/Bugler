---
status: accepted
---

# The MCP tool set is a shape of its own, not a mirror of the REST API

Bugler's REST API was written for exactly one client, its own SPA: counts that stop at a thousand,
cursor pagination, and DTOs that carry every resource attribute of every row because a browser table
can hide what it does not show. Wrapping those endpoints one for one would be the cheapest MCP
server to write and the dearest to use. An agent does not scroll, so `before`/`beforeId` is a wasted
turn; a hundred Log Records with their attributes is one screenful to a person and tens of thousands
of tokens to a model, most of them `telemetry.sdk.version`; and a Volume's Buckets are numbers to
draw, not to read.

So the tools are designed rather than derived. Eight of them — the Catalog, Episodes and one
Episode, a search over Log Records and one Log Record, the Observed Keys a Filter can be built from,
Releases, and a Trace flattened to Spans carrying their parents. Each is named for the deed and
described in the ubiquitous language, because the glossary in every `CONTEXT.md` is already the
English prose a model needs in order to use a tool correctly — the one advantage this repo has here
that most do not. Their answers are budgeted in tokens rather than screenfuls: fifty records by
default and two hundred at most, attributes returned in a list only where the Filter named them and
in full only for a single record asked for by id.

And they **never truncate in silence**. Every answer states how many records the Filter matched,
capped as the REST count is. `Follow` may pass over records without saying so (Exploration ADR 0004)
because the person watching a stream knows they are watching one; an agent handed fifty of four
thousand errors, and told nothing, will conclude in writing that the problem is marginal.

## Considered Options

- **One tool per endpoint.** Nearly mechanical to build, and it inherits every shape that exists
  because a filter panel and a human eye were on the other end.
- **Resources and Prompts alongside Tools.** The glossary already rides in the tool descriptions,
  where every client sees it, while Resources are supported unevenly across clients; and a Prompt
  would be Bugler's opinion about how debugging goes — a workflow it cannot see, belonging in the
  user's own instructions rather than in the server's.
- **A trace list and a Volume summary in the first set.** A list of Traces without a Waterfall tells
  an agent nothing a Log Record would not, and its paging is a decision of its own (ADR 0026); a
  trend is two searches and the counts they already carry. Both can be added when they are missed.

## Consequences

- **The glossary becomes partly a public contract.** A term the tools carry cannot be renamed
  without configured clients seeing it. Holding the language still is what a ubiquitous language is
  for, but it was documentation until now and is an interface from here on.
- Tool answers and REST answers may drift in shape. They are two faces of one read model, and the
  read model — not the two faces — is where they are obliged to agree.
- Registry and Access are not served at all. API keys, users, grants, retention and AI Consent stay
  out of reach: this door answers for telemetry, never for the administration of the server.
