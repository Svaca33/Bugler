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
