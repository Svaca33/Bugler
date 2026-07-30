---
status: accepted
---

# A Reset Ticket is a row, not a signed token

A Password Reset is proven by a Reset Ticket: 32 random bytes that leave in the mail and a row in
`access.reset_tickets` holding only their SHA-256 fingerprint, a User, an expiry an hour out, and
the moment it was spent. Redeeming one writes the new password and marks the ticket consumed;
issuing one deletes whatever tickets that User had, so the newest mail is always the one that works.

The obvious alternative was a self-describing token — ASP.NET Core's `DataProtectorTokenProvider`,
or a bare `ITimeLimitedDataProtector` — which needs no table at all. It was rejected on a fact about
this deployment rather than on principle: **Bugler configures no Data Protection key persistence and
its container mounts no volume for one**, so the key ring is regenerated on every image build. A
signed token would stop working at each redeploy, and the person holding a twenty-minute-old mail
would be told their link is invalid with no way to find out why. Making that option work would mean
adding key persistence to the deployment first — a moving part Bugler does not have today.

## Considered Options

- **`UserManager.GeneratePasswordResetTokenAsync`.** The stock answer, and its single-use property
  is real: the token embeds a security stamp that the password change then rolls. But the method
  hangs off `UserManager<TUser>`, so taking it means bringing in `AddIdentityCore`, an `IUserStore`,
  and a second model of a user beside the `User` this context owns. Access uses exactly one type
  from Identity — the password hasher — and that is the right amount.
- **`ITimeLimitedDataProtector` over the user id.** No `UserManager`, no table, but no revocation
  either: a ticket cannot be spent, cannot be voided when a newer one is issued, and cannot be
  refused after the password has already changed. Every rule below would have to become a
  convention nobody could enforce.
- **Storing the secret rather than its fingerprint**, so the mail could be composed again later and
  a durable queue could retry it. Rejected: a table of live reset secrets is a table of live
  passwords. The consequence — that a failed send cannot be retried — is accepted below instead.

## Consequences

- The secret exists only inside the request that issued it. **No durable retry of the reset mail is
  possible**, by construction: it is handed to the in-memory mail queue (ADR 0011) and if that fails,
  it is gone. Asking again two minutes later is the whole recovery path, and it is enough.
- Redemption ends **every** Session of the User, sparing none — unlike a Password Change, which
  keeps the browser it was performed in. Somebody who needed this link was holding no Session worth
  keeping.
- A successful reset is not followed by a sign-in. Sessions are minted in one place, by signing in,
  and that is also where "stay signed in" is chosen; the reset page has no business deciding it.
- Asking for a link answers the same sentence for a real address, an unknown one and a deactivated
  account. The one thing the API does admit is that a server without SMTP cannot reset at all —
  that says nothing about any User, and hiding it would promise mail that never comes.
- Issuing is throttled to one mail per account every two minutes. The threat it answers is not
  account guessing but somebody filling a colleague's inbox by holding down a button; per-account
  is where that can be counted, and the row was being looked up anyway.
- Nothing sweeps the table on a schedule. Issuing deletes that User's old rows, which is the whole
  of the housekeeping — what remains is a handful of spent tickets belonging to people who never
  asked again.
- No log records that a password was reset, or from where. Bugler keeps no audit (ADR 0007) and this
  is not the place to start one.
