---
status: accepted
---

# Attempts are counted per address, not per caller

`/api/auth/login` and `/api/auth/password/forgot` now spend an **Attempt Budget** held against the e-mail address the request names: ten sign-in attempts with one returning every thirty seconds, three asks for a reset link with one returning every two minutes. A spent budget answers `429` with `Retry-After` and nothing else. The other three endpoints that answer before anyone is signed in — `status`, `setup` and `password/reset` — are left alone.

The budget is keyed on the address in the request body and never on where the request came from. That is the whole decision, and it is the opposite of the usual one.

**Why not the caller's address.** Bugler sits behind a proxy that terminates TLS and speaks plain HTTP to Kestrel, so every request arrives from the same connection address — the proxy's. A budget kept per connection would count a whole company as one guesser and refuse them together, which is not a limiter but an outage waiting for its first attacker. Reading `X-Forwarded-For` instead would mean trusting it, and ADR 0019 already worked through what that costs: the headers must be accepted from the proxy alone, ASP.NET trusts only the loopback by default, and the shortest way to make it work in a container is `KnownNetworks.Clear()`. Here that is worse than it was for the cookie. A forwarded address anybody may assert is a limiter anybody may evade by rotating a header — and, worse, one anybody may aim at a colleague's address on purpose. The knob whose easiest setting is unsafe would this time make the feature both useless and abusable.

**What the address buys instead.** Guessing pays only where it is aimed, and it can only be aimed at an account. Counting per address bounds exactly the thing worth bounding: the server does at most ten PBKDF2 verifications per address per burst, whoever asks and from wherever. It cannot be evaded, because the attacker cannot vary the one field they need to keep constant — and spelling it differently does not help, because an address becomes a budget's name through the same normalisation the handler looks a User up by.

**What it costs, said plainly.** Anybody who knows a colleague's address can spend that colleague's budget on purpose. There is no arrangement of a per-account limiter that avoids this; the choice is only how expensive it is to do and how long it lasts. A token bucket is what makes it cheap to survive rather than cheap to inflict: the budget refills continuously, so a spent one is worth retrying in half a minute rather than at the top of some window. And the refusal never says whose budget it was — a budget exists for every address that asks, including addresses belonging to nobody, so a `429` is not an answer to "does this account exist".

**Why not a global limit as well.** A ceiling over the whole anonymous group is the one shape that hands a single caller the power to close the sign-in page for everybody, and `/api/auth/status` is fetched by every browser that loads the SPA — behind the proxy, from one address. What remains unbudgeted is an indexed lookup on `reset`, an `AnyAsync` on `setup` and `status`: a flood of those is an HTTP flood, which is the proxy's business, like TLS and HSTS before it.

**Why it is not middleware.** ASP.NET Core's rate limiter partitions on `HttpContext` through a synchronous partitioner that runs before model binding, so it can reach the connection, the headers, the path and the signed-in principal — everything except the body, which is where our key lives. Using it would have meant a middleware that buffers the request, parses the JSON to find the address, stashes it, and rewinds the stream so the handler can parse it again. The budgets are spent in the handlers instead, on the first line, before any query and long before the hasher — which is exactly where the middleware would have refused them.

## Considered Options

- **`UseForwardedHeaders` with `KnownProxies`, then count per client address.** The textbook answer. Rejected above: it is a trust knob whose easiest setting turns the limiter into something an attacker controls, and it would have to be got right by every operator to be worth anything at all.
- **A fixed window rather than a token bucket.** Rejected for the shape of its recovery: an address whose budget was spent maliciously waits out a whole window, and the boundary between two windows lets twice the burst through back to back.
- **Delaying the refused request instead of answering it.** It has a real virtue — a legitimate caller always gets in, just slowly, so nobody can be locked out at all. Rejected because the delayed path is the *attacker's* normal path: they would hold thousands of sleeping requests open, and we would have traded one account's lockout for the server's connections, which is cheaper to attack and does not require knowing anybody's address.
- **Locking the account after N failures and saying so.** Rejected twice over: it says whether an account exists, and it hands anybody a way to end a colleague's day rather than their next thirty seconds.

## Consequences

- The budgets live in memory. A restart hands every address a full one, which is acceptable because nobody outside can order a restart, and it is the only storage that costs a guess nothing to consult. It also means the count is per process, which in a single-process monolith (ADR 0002) is the same as per server.
- Addresses nobody has asked about lately are dropped by the partitioned limiter itself, so a spray across invented addresses costs memory only while it lasts.
- `429` is a new answer from two endpoints that only ever spoke `200`, `401`, `404` and `202`. The sign-in form and the reset form both name it, so a caller who has been counted is told to wait rather than told nothing.
- Bugler still rate-limits nothing by address, so a deployment that wants a ceiling on raw request volume asks its proxy for one. DEPLOYMENT.md says so where it already divides the labour over TLS.
- The e-mail addresses being counted are held in memory for as long as their budgets are, in a process whose database holds the same addresses. Nothing is written down and nothing is logged.
