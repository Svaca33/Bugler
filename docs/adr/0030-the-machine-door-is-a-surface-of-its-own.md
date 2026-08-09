---
status: accepted
---

# The machine door is a surface of its own

Bugler serves its audiences on separate listeners and calls each one a Surface: the app on 8080,
OTLP on 4317 and 4318, so that a telemetry producer can never reach the UI and a browser can never
reach the receivers. MCP is a fourth audience — a tool holding a Machine Delegation, speaking HTTP,
never a browser and never a cookie — and it gets its own `Surface` and its own listener rather than
a path on the app.

What decides it is `SameOriginMutations`. The app surface refuses, **before routing**, any non-safe
request that does not carry the `Bugler-Request` header, and ADR 0025 mounted it there precisely so
that no endpoint on that surface could be added without it. MCP's Streamable HTTP transport is
POST. Serving it at `/mcp` on 8080 would mean cutting the first hole in an invariant built to be
uncuttable; serving it on a listener of its own needs no exception at all, because the sentence
already written for the OTLP surfaces applies to it verbatim — authenticated by a credential and
never by a cookie, so nothing there is at stake.

The split earns a second thing that no in-process switch could: the operator may leave the MCP port
unrouted while the UI stays public. Above it stands an **Admin-only server switch, off by default**
— whether this Bugler opens a machine door at all — on the same terms as the SMTP and AI settings
(ADR 0014, ADR 0027). Beside it sits the address the door answers at (`Server:PublicMcpUrl`),
because how a server is reachable from outside is something only its operator can state (ADR 0019);
a port swapped into `Server:PublicBaseUrl` would be a guess, and a guessed address inside a command
somebody copies fails far from where it could be corrected. Unlike `PublicBaseUrl` it decides
nothing but the text of that command — no cookie, no link, no TLS conclusion — so the one place
that rules on security stays one place.

That setting is the **origin only**; the path is added when the command is written. The division is
not tidiness. Where this Bugler is reachable is the operator's fact and nothing else can know it,
while where the door sits on Bugler is Bugler's own and the operator has no reason ever to have
heard of it. Asked for the whole address, an operator gives the half they know — and the command
points, in silence, at the root.

## Consequences

- One more port in `docker-compose.yml`, in the README, and in every operator's reverse proxy. That
  is a real tax on a self-hosted tool and it is paid knowingly.
- Clients keep the address in their configuration, so **moving this surface later breaks all of
  them**. It is settled now rather than deferred.
- `/health` answers on this listener as on every other, so MCP is mounted at `/mcp` and not at the
  root — whoever puts this behind a proxy will want to probe it.
- The transport runs stateless: the door is read-only, nothing is ever pushed from server to
  client, and no session state is held between calls.
