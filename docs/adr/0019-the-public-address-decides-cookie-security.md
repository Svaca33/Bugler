---
status: accepted
---

# The public address decides the Session cookie's security

The Session cookie carries `Secure` and the `__Host-` prefix when `Server:PublicBaseUrl` names an https address, and neither when it does not. Bugler terminates no TLS of its own — Kestrel listens on plain HTTP and whatever stands in front holds the certificate — so from inside the process no request ever arrives over HTTPS. ASP.NET's default `SameAsRequest` policy is therefore a condition a correctly deployed server can never meet: it hands out an unprotected Session precisely where the deployment is right. The public address is the one thing Bugler already knows about itself that says otherwise, and an operator has to state it correctly regardless, because every link Bugler mails is built from it and password reset is refused without it.

Four readings of that string, and the last two are the same decision said twice: https means TLS stands in front; plain HTTP on a loopback address means a developer's machine; plain HTTP on a name others resolve means an exposed server; anything that is not an absolute http or https URL means the server has not been told where it lives. Only the first asks the browser for anything.

## Considered Options

- **`UseForwardedHeaders`, and let the proxy declare the scheme.** The general answer, and the one a reader will suggest again. Rejected because it does not remove a trust decision, it adds one: the headers must be accepted from the proxy alone, and ASP.NET trusts only the loopback by default — which inside a container the proxy is not. The operator would have to name `KnownProxies` or `KnownNetworks` correctly, and the shortest way to make it work is `KnownNetworks.Clear()`, after which anybody who reaches Kestrel can assert https and mint themselves a Secure cookie. A second knob, whose easiest setting is the unsafe one.
- **An explicit `Access:Cookie:SecurePolicy` setting.** What DEPLOYMENT.md promised for a while and Bugler never had. Rejected for describing the same truth twice: "where I am reachable" and "do I have TLS" would be two settings that can disagree, and one of them would eventually be wrong while the other looked right.
- **`CookieSecurePolicy.Always`, unconditionally.** Browsers treat `localhost` as a secure context, so development would most likely survive it. Rejected for leaning on that exception to hide the fact that every plain-HTTP deployment on a real hostname would break at sign-in, in the browser, where no explanation reaches anybody.
- **Reading an unstated address as https anyway, to fail closed.** Rejected on the same ground: guessing produces a cookie the browser silently drops, and the failure surfaces as a login that succeeds and a next request that is 401. The unstated state is prevented instead — `docker-compose.prod.yml` refuses to start without `BUGLER_PUBLIC_BASE_URL` — and warned about where it survives.

## Consequences

- `Server:PublicBaseUrl` is no longer only about links. A typo in it, or an http address where the deployment serves https, is now a security setting set wrongly rather than a cosmetic one — and it is the value the reset mail already depended on, so it is not a new thing to get right.
- The cookie is named `__Host-bugler.session` wherever the policy is `Always`. The prefix makes the browser refuse it unless `Secure` is set, the scope is `/`, and no `Domain` is named, so those flags stop being a promise this code makes and become one the browser enforces — and no sibling host under the same registrable domain can plant a cookie of that name. It also means the name changes with the deployment, and everyone is signed out once when a server first runs a version that mints it.
- Bugler says at startup which of the four readings applies, and warns on the two that are wrong. Nothing else about this fails loudly: an unprotected Session cookie is served, accepted and used exactly like a protected one.
- The integration harness now takes its public address as one fact rather than two. A server told it lives at an https address mints a Secure cookie, and `CookieContainer` will not send one back over `http://` — so a test client whose base address disagreed with the server's would quietly stop being signed in.
- HSTS and the HTTP-to-HTTPS redirect remain the proxy's business. Bugler emits no header about a transport it does not hold.
