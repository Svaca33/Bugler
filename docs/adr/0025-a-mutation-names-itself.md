---
status: accepted
---

# A mutation names itself

Every request on the App surface that is not `GET`, `HEAD`, `OPTIONS` or `TRACE` must carry the
header `Bugler-Request`. The value is not read — presence is the whole test. The SPA sets it beside
the `Accept-Language` it already sets, and anything without it is refused with `403` before routing.

**What it answers.** The Session cookie is `SameSite=Lax`, which is most of a CSRF defence and not
all of it: it stops the cookie riding along on a cross-site `POST`, but it is one layer, enforced by
the browser, and Bugler's own posture said nothing. Most mutations here take a JSON body, which is a
second obstacle a plain `<form>` cannot clear — but the body-less ones could not even lean on that:
signing out, acknowledging an Episode, solving one, revoking an API key, deleting a User, resetting
the SMTP settings. `setup` is the sharpest of all, because a cross-site `POST` to an unclaimed
server would hand the attacker an Admin account.

**Why a fixed header and not a token.** A token is worth more than a static header against exactly
one attacker: one who can set arbitrary headers cross-origin. That takes a permissive CORS policy,
and Bugler registers none — the UI and the REST API share one origin deliberately, so no CORS
policy is wanted here now or later. Without one, a cross-origin `fetch` carrying a header of
Bugler's choosing earns a preflight that goes unanswered, and a `<form>` cannot send headers at all.
Against every attacker that leaves, a constant is exactly as good as a token, and it costs no token
endpoint, no second cookie with `HttpOnly` turned off, and no state.

## Considered Options

- **`AddAntiforgery` with a token.** The framework's answer, and the one a reader will reach for
  again. Rejected above on what the token buys; and it is machinery built for MVC forms — the
  conventional header names it ships with (`X-CSRF-TOKEN`, `X-XSRF-TOKEN`) are the `X-` prefix
  RFC 6648 retired, so even taking it would mean renaming the header anyway.
- **Checking `Origin` / `Sec-Fetch-Site`.** Cheaper still: no client change at all. Rejected on what
  it has to assume. An absent `Origin` must be let through or every non-browser caller dies, and
  what a present one is compared against is either `Server:PublicBaseUrl` — one more way a
  misconfigured address breaks sign-in, on top of ADR 0019 — or the `Host` header, which a proxy may
  have rewritten. The header assumes nothing.
- **Requiring it only where a Session is required.** Rejected for the exception list it creates.
  Login CSRF is real (an attacker signing a visitor into the attacker's own account, then reading
  what they do there) and `setup` is worse, so the anonymous doors need it most — and "every unsafe
  method, no exceptions" is a rule nobody has to remember when adding the next endpoint.

## Consequences

- Anyone driving the REST API with a script and a cookie must send the header too. `README.md` and
  `DEPLOYMENT.md` say so. OTLP senders are untouched: they authenticate with an API key on 4317 and
  4318, where no cookie and no browser ever reaches.
- The header is **not** written into the OpenAPI document as a parameter. It would land in the
  generated client as a required argument on every mutation, when the client sets it once in an
  interceptor — noise in `schema.d.ts` for a fact that is true of the whole surface, not of any
  operation.
- The refusal is `403` with an English sentence outside the message catalogues. A `401` would make
  the SPA treat a missing header as an expired Session and bounce to sign-in, looping; and the
  sentence is only ever read by somebody holding a script, which makes it machine-facing text like
  `/health` (ADR 0024).
- The check runs before routing, so it also covers a `POST` to a path that has no endpoint. That is
  intentional and free.
- Every test client that mutates has to send it, the integration harness included. That is the same
  one-line change the SPA made, and it means the tests exercise the surface as it really is.
