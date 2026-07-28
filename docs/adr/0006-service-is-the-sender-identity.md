---
status: accepted
---

# A registered Service is the identity of a telemetry sender

The identity of everything Bugler stores is the Service it came from: a registered entry holding an Application, a `service.namespace`, a `deployment.environment.name` and a `service.name`, owning the API keys that authenticate its exports. What a payload claims about itself — including its own `service.name` — is kept as an ordinary resource attribute and never establishes identity, because only the API key is provable. This replaces the earlier split in which a registered Instance and a self-declared `service.name` were two competing answers to "who sent this", stored side by side on every signal.

## Considered Options

- **Instance as one client deployment, several services under one key** (the original model) — matches how an installation is provisioned, but leaves every signal with two names, one authenticated and one not, and forces a leaked mobile key to be shared with the backend that cannot then be revoked independently.
- **The payload decides and the registry only holds credentials** — gives auto-discovery of new services, but one key can then mint unlimited identities, which is the ambiguity this decision exists to remove.
- **Registration declares the expected `service.name`, mismatches are rejected** — catches misconfiguration, but a typo in `OTEL_SERVICE_NAME` would silently discard telemetry; going blind exactly when something changed is a worse failure than a wrong label.
- **A Deployment level between Application and Service** — normalises namespace and environment out of the Service, rejected as premature. Promoting to it later is a mechanical regrouping of existing Services.

## Consequences

- One API key per Service, so a deployment running a backend and a mobile client registers twice and can revoke either without touching the other. Several keys may be valid at once, so rotation has no gap — which is what lets a mobile client fetch its key from the backend at sign-in instead of carrying one baked into the build.
- Replicas of a Service share its registration and key, and are told apart only by the self-declared `service.instance.id`. Senders must set it to something stable; Bugler can neither enforce nor repair it.
- Stored signals carry no service name of their own: the name follows from the authenticated Service, while the sender's claim survives among the resource attributes, so a mismatch stays visible instead of overwriting the truth.
- Filters address Services by facet — namespace, environment, name — rather than by picking one registration, so "everything from demo/prod" and "every mobile client" are each a single filter.
- Per-customer read access remains impossible: grants are per Application, and the customer axis is a facet of the Service rather than a grantable entity. Wanting it is the trigger for introducing the Deployment level.
