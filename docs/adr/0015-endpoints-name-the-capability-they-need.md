---
status: accepted
---

# Endpoints name the capability they need, not the role that grants it

Authorization here started, correctly, as one bit: a User either is an Admin or is not, and the
endpoints that change how the server behaves ask for the `"Admin"` policy. That bit is going to
grow into roles holding named permissions. When it does, the migration cost sits entirely in how
the endpoints *spell* what they want: `RequireAuthorization("Admin")` says who the caller must
be, so every one of them has to be rewritten the moment the answer stops being a role. A policy
named for the deed — `ConfigureAlerting` — says what is being asked for, and only its definition
has to change.

So new endpoints name capabilities. `Bugler.Access.Contracts.Capabilities` holds the names,
`AccessModule` defines what each currently means (today: `RequireRole("Admin")`, verbatim), and
the frontend asks the same question through one helper per capability rather than reading
`isAdmin` at each control. Nothing about who may do what changes on the day this lands.

Adoption is deliberately partial. The Alerting admin group asks for `ConfigureAlerting`;
Registry's admin group, Access's user administration, and the Host's mail settings still ask for
`"Admin"`, and both spellings are registered side by side. A repo-wide rename would be a large
diff whose only reader is a permission system that does not exist yet, and which would want to
decide its own catalogue of capability names — a catalogue guessed now, from one feature, would
likely be wrong. The rule is therefore forward-looking: what is written next names a capability;
what exists is converted when the permission model arrives and can say what the right names are.

## Consequences

- Two spellings of the same authorization coexist, and a reader of `AlertingModule` sees a
  different policy name than a reader of `RegistryModule`. Accepted: the inconsistency is
  visible, documented, and points the way the code is going.
- The capability names are not a public contract. Renaming one is an internal change until a
  permission model gives them meaning outside the code.
- The client's answer may drift from the server's, since it computes capability from `isAdmin`
  rather than being told. That drift is bounded by keeping every such computation in one module
  (`frontend/src/lib/capabilities.ts`); the server remains the only enforcement.
