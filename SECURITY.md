# Security

Bugler is self-hosted: it holds your telemetry on your machine, behind your proxy, next to your
database. That makes a weakness in it your problem before it is anyone else's, so reports are taken
seriously even though the project is small.

## Reporting a vulnerability

**Please do not open a public issue.** Use GitHub's private reporting instead:

> **Security** tab → **Report a vulnerability**

That opens a private thread visible only to you and the maintainer. Include the version, how Bugler
is deployed, what an attacker gains, and the smallest sequence that shows it.

What to expect, stated honestly: this is a one-person project, so there is no guaranteed response
time and no bounty. You will get an acknowledgement, a plain answer about whether it is being fixed,
and credit in the published advisory unless you would rather not be named.

## What is in scope

Anything that lets somebody read telemetry they were not granted, act as another user, get code or
queries to run, or take the server down with input a normal sender could send.

Out of scope: an attacker who already runs code on the host or reads the database directly, and
anything that follows from a deployment choice Bugler documents and warns about — chiefly running
without TLS in front, which [DEPLOYMENT.md](DEPLOYMENT.md) and
[ADR 0019](docs/adr/0019-the-public-address-decides-cookie-security.md) discuss.

Weaknesses the project already knows about are tracked as ordinary issues, in the open, and labelled
`security`. Finding one of those documented rather than hidden is deliberate; you are still welcome
to say it matters more than the label suggests.

## Supported versions

The current release line is the supported one. Fixes are not backported: upgrade to the newest
version, which is why the deployment guide pins a version rather than `latest` and why upgrading is
one line and a restart.
