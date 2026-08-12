# Access

Human identity and authorization: who can sign in to Bugler and whose telemetry they may read. The exclusive owner of the concept of a user.

## Language

**User**:
A person with a local Bugler account (e-mail + password).
_Avoid_: member, account, login

**Admin**:
A User who manages the server — users, grants, and the Registry — and reads all telemetry without needing grants. The first User of a server becomes Admin. An Admin may not deactivate or delete their own account, so a server never runs out of Admins.
_Avoid_: superuser, owner, root

**Deactivation**:
The reversible withdrawal of a User's access: they can no longer sign in, but everything the account holds is kept for their return. Undone by reactivating them.
_Avoid_: suspension, disabling, locking, soft delete

**Deletion**:
The permanent removal of a User together with their Application Grants, freeing their e-mail for a new account. Not a stronger Deactivation but a different answer: an end rather than a pause, and neither one is a step towards the other.
_Avoid_: purge, offboarding, archiving

**Language**:
The language Bugler speaks to a person — the UI, a mail, a refusal — carrying its formatting
conventions with it. A User may choose one for themselves; while they haven't (null), they follow
whatever the server speaks, today and after an admin changes it. Machine-facing text (logs,
health answers, severity band names) is not spoken in a Language at all.
_Avoid_: locale, culture, region, i18n setting

**Application Grant**:
The permission for one User to read the telemetry of one Application. References the Application by id only, outlives the User's Deactivation, and dies with their Deletion.
_Avoid_: permission, role assignment, ACL entry

**Session**:
An authenticated sign-in of a User, lasting until they sign out, until it expires, or — unless the
User chose to stay signed in — until they close the browser. A Session is revalidated against the
User behind it on each request, so deactivation, deletion or a new password ends it and role changes
reach it without a re-login. The Session the password was changed from is the one exception: it
survives, because throwing somebody out of the browser they just used would say nothing about who
they are.
_Avoid_: token, login state

**Sign-out**:
A User ending their Sessions — all of them, in every browser they hold one, not only the one that
asked (ADR 0003). Nothing about the account changes; only which Sessions still count.
_Avoid_: logout as a fact about one browser, session close

**Machine Delegation**:
A User's own reading, lent to a tool on their machine: it reads telemetry in their name, never past
their Visibility Scope, never wider than the one Application it may be narrowed to — and it writes
nothing beyond the machine hand's narrow Alerting verbs, and those only when its Grade grants them.
The Grade is one of two — reading alone (the default), or reading and the machine hand — and like
the narrowing it is **stamped in at issue and cannot be edited**. Proven by a Secret shown once at
issue and never restorable, read back against the User behind it on every request — so Deactivation
and Deletion end it at once, while a Password Change does not, because unlike a Session it was never
minted from a password. Worth holding for a fixed span rather than forever: its Secret lives in a
configuration file on somebody's laptop, and time is what bounds a leak nobody noticed. The
Application, the Grade and the span are fixed at issue — wanting different ones means revoking it
and issuing another, which is what makes it a credential rather than a setting. Not an identity of
its own: nothing is delegated that the User does not already hold, and nothing survives them.
_Avoid_: token, PAT, personal access token, service account, machine user, API key, bot, access rule

**Password Change**:
A User replacing their own password while signed in, proven by the password they are replacing.
_Avoid_: reset, update

**Password Reset**:
Setting a new password without knowing the old one, proven by holding a Reset Ticket. Ends every
Session of the User, because whoever asks for one is by their own account holding none.
_Avoid_: recovery, forgotten password

**Attempt Budget**:
What one e-mail address may spend on the doors that answer before anyone is signed in: signing in,
and asking for a reset link. It belongs to the address rather than to a User — every address that
asks has one, including addresses belonging to nobody, so a spent budget never tells whoever asked
whether an account is behind it. It refills continuously rather than at the turn of an hour, so
spending somebody else's on purpose costs them a moment and never their account.
_Avoid_: rate limit, throttle, lockout, ban

**Evened Answer**:
An answer from the doors that reply before anyone is signed in, released only once a fixed floor
of time has passed — so how long Bugler thought says nothing about what it found. The text of
these answers was evened first; this evens the clock.
_Avoid_: constant-time response, timing padding, delay

**Reset Ticket**:
One User's single-use permission to set a new password, worth presenting for an hour. Its secret
exists only in the mail that carried it — Bugler keeps a fingerprint, enough to recognise the ticket
and not enough to forge one. Issuing one voids the User's previous ticket, so the newest mail is
always the one that works. An Admin may hand a ticket over directly instead of mailing it, for a
server that cannot send mail at all.
_Avoid_: token, link, code, one-time password
