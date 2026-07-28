# Access

Human identity and authorization: who can sign in to Bugler and whose telemetry they may read. The exclusive owner of the concept of a user.

## Language

**User**:
A person with a local Bugler account (e-mail + password).
_Avoid_: member, account, login

**Admin**:
A User who manages the server — users, grants, and the Registry — and reads all telemetry without needing grants. The first User of a server becomes Admin.
_Avoid_: superuser, owner, root

**Application Grant**:
The permission for one User to read the telemetry of one Application. References the Application by id only.
_Avoid_: permission, role assignment, ACL entry

**Session**:
An authenticated sign-in of a User, lasting until logout, expiry, or — unless the User chose to
stay signed in — until they close the browser.
_Avoid_: token, login state
