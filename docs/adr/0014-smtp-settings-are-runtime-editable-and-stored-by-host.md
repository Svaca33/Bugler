---
status: accepted
---

# SMTP settings are runtime-editable and stored by Host

SMTP can now be configured on the admin screen (Administration → Server) and the values live in
a single-row table, `server.smtp_settings`, owned by **Bugler.Host** — not by any bounded context
and not by the Mail transport. The precedence is a switch, not a merge: from the moment anything
was saved on the screen, the stored row is the whole truth and the `Mail:Smtp` configuration
section is ignored; the screen's reset action deletes the row and the configuration section
applies again. Which side is answering is always shown on the screen.

The transport asks `ISmtpSettingsSource` (defined in `Bugler.Mail`) for the settings at every
send, so a save applies to the very next message without a restart. `Bugler.Mail` registers a
configuration-backed source; the Host replaces that registration with the stored one, which falls
back to it while the table is empty. Everybody who used to peek at `MailOptions` to ask "can this
server mail at all?" — Access hiding the reset link, Alerting deciding whether to owe mail
deliveries — asks the source now, for the same reason: the options only know the configuration
branch.

The prompt was an operator whose relay is a bare IP on the LAN: no TLS, no account, port 25.
That also made the security mode explicit (`Automatic`/`None`/`StartTls`/`ImplicitTls`): the old
hardwired StartTlsWhenAvailable could neither promise "plaintext on purpose" to a relay
advertising STARTTLS with a broken certificate, nor refuse a downgrade.

## Considered Options

- **Keep SMTP in configuration only.** No new storage, but "edit an env var and bounce the
  container" is exactly the ceremony a UI exists to remove, and the operator asking for this
  cannot edit the deployment.
- **A new bounded context for server settings.** The full ceremony (CONTEXT.md, schema, arch
  tests) for one form. Host already owns deployment topology — an SMTP relay is deployment
  topology. If runtime-editable server settings multiply (PublicBaseUrl is the obvious next),
  promote the store to a context then.
- **The Mail transport owns its settings table.** Shortest path, but it breaks ADR 0011's load-
  bearing sentence — the transport "owns no data, has no lifecycle". With Host owning the row,
  that sentence stays true.
- **Field-level merge between the row and configuration.** Rejected for being unexplainable:
  "which value is live" would have seven answers instead of one.
- **Encrypting the stored password.** The keys would live beside the ciphertext — in this
  process's Data Protection ring (today ephemeral: every container rebuild would silently break
  mail, the exact failure mode the Server screen exists to expose) or in the same database as the
  ciphertext, where a backup carries both. Theatre, so: stored in clear, like it already stood in
  the compose file's environment, and **write-only through the API** — responses carry only
  `hasPassword`; saving sends `null` to keep, `""` to clear.

## Consequences

- `server` joins the schemas; Host gains its own `ServerDbContext`, migrated at startup like the
  contexts', and the first EF packages of its own.
- The test button on the Server screen tests the **saved** configuration, never the form's
  unsaved edits — a green test proves what an alert would actually use, and the write-only
  password could not ride along anyway.
- `Mail:Smtp` in configuration keeps working for existing deployments and for docker-compose's
  mailpit, but only until somebody saves the form; the compose file says so.
- An admin can now see and change where all of Bugler's mail goes — subscribers included — which
  is the point, but it makes "Admin" the only gate on the relay. The endpoint group already
  stands behind that policy.
