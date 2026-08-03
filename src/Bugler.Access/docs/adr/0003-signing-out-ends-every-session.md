---
status: accepted
---

# Signing out ends every Session

`/api/auth/logout` rolls the User's Security Stamp before it deletes the cookie, so every Session
that User holds — in this browser, on their phone, and in whatever they left signed in at the
office — stops counting on its next request. Signing out is no longer a fact about one browser.

**What the old logout ended.** Nothing on the server. It called `SignOutAsync`, which deletes the
cookie in the browser that asked, and the ticket inside that cookie is self-contained: Bugler keeps
no session store, so a copy of it taken from anywhere stays valid until its seven days run out.
Session Revalidation already caught deactivation, deletion and a changed password through the
Security Stamp — but "I signed out" was not among the things a Session was answerable to. The one
remedy for somebody who suspects they left a Session behind was changing their password, and
nothing told them that is what it takes.

**Why the Stamp rather than a session store.** The Stamp is the mechanism this context already has
for ending Sessions, and it is read back on every request anyway (`SessionRevalidationSeconds`
defaults to zero). Rolling it costs one `UPDATE` and no new query, no table, and no second idea of
what makes a Session valid. It also answers the *whole* of what the sign-out gap was: both the
copied ticket and the Session forgotten in another browser die together, because the Stamp does not
know which Session asked.

## Considered Options

- **A per-Session revocation list** — a Session id in the claims and a table of ended ones, joined
  into the read-back that already happens. Precise, and it would let one browser sign out without
  disturbing another. Rejected because precision is the wrong goal here: it answers the copied
  ticket and leaves the forgotten Session in the other browser exactly where it was, which was half
  the reason to do this. It also buys a migration, a claim, and a table somebody has to sweep.
- **A server-side ticket store (`ITicketStore`).** Full control over every issued Session, at the
  price of a write per sign-in, a read per request, and the end of a self-contained cookie. Bought
  for a need Bugler does not have: it holds a handful of accounts, not a fleet of devices per user.
- **Leaving logout as it was and documenting the password change as the remedy.** Rejected as
  documentation standing in for a mechanism — the remedy is drastic, undiscoverable, and rolls the
  same Stamp this decision rolls directly.

## Consequences

- **Signing out on one device signs the User out everywhere.** Accepted deliberately: for a
  self-hosted tool this is closer to what people mean by the word than the alternative, and the
  cost — signing in again on the phone — is small next to a Session that outlives its owner's
  intent. The button keeps its plain label; the promise is written down here and in `CONTEXT.md`.
- The effect is immediate only while Sessions are revalidated on every request, which is the
  default. An operator who raises `Access:SessionRevalidationSeconds` buys back a window in which
  the other Sessions read on — the same window that already delays a deactivation.
- The Stamp is no longer a fact about passwords. It says which generation of Sessions counts, and
  `Passwords.Set` is one of two things that move it rather than the only one. What must *not* move
  it is a rewrite of the same password at a new iteration count — see `Passwords.Rehash`.
- A logout whose write fails answers with an error and leaves the caller signed in. The cookie is
  deleted last on purpose: the alternative is a browser that looks signed out while the Sessions it
  asked to end are still good.
- Nothing records that a sign-out happened. Bugler keeps no audit, here as elsewhere.
