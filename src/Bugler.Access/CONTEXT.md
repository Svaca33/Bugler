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

**Application Grant**:
The permission for one User to read the telemetry of one Application. References the Application by id only, outlives the User's Deactivation, and dies with their Deletion.
_Avoid_: permission, role assignment, ACL entry

**Session**:
An authenticated sign-in of a User, lasting until logout, expiry, or — unless the User chose to
stay signed in — until they close the browser. A Session is revalidated against the User behind it on
each request, so deactivation, deletion or a new password ends it and role changes reach it without
a re-login. The Session the password was changed from is the one exception: it survives, because
throwing somebody out of the browser they just used would say nothing about who they are.
_Avoid_: token, login state

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
