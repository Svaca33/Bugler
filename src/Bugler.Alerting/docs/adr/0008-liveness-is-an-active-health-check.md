---
status: accepted
---

# Liveness is an active health check, not detected silence

A Service that stops exporting altogether — crashed, descheduled, network cut — was invisible to
Alerting: no Log Records means no Match, no Episode, no Alert, and silence read exactly like
health. The obvious remedy was to make absence itself observable: Registry knows which Services
exist, so one that has sent nothing for longer than expected could open an Episode.

That was rejected in favour of **asking**. A Service may hold one Health Check address; Bugler
calls it on the alerting loop's beat and three consecutive failures open an Episode. Inferring
death from silence needs an expected reporting interval, and there is no honest way to get one: a
setting is a number nobody can pick correctly for a service that logs hourly by design, and
deriving it from observed traffic turns every quiet Sunday into an outage. An answer to a direct
question has no such ambiguity.

**The gap this leaves is real and accepted**: a Service whose API key was rotated badly answers its
health check perfectly while nothing reaches Bugler at all. Active probing says "the process is
up", not "the telemetry is arriving". If that failure ever needs covering, it is a third Watch and
not a change to this one.

The verdict is the **status code alone** — 2xx alive, everything else (including a redirect, which
is why the client does not follow them) not. Reading the body would mean adopting one framework's
health contract, and a Service in Go or Python would have to impersonate ASP.NET to be believed;
`Degraded` deliberately does not open an Episode, because "it is unwell" is what the logs are for.
Not reading the body also bounds what a misconfigured address can reveal to a single bit, which is
the whole answer to the SSRF question — that, and the fact that only an Admin of a self-hosted
Bugler can set it, and that blocking private address ranges would kill the ordinary case
(`http://backend:8080/health` on the same Docker network).

Three failures rather than one is not the "N errors in M minutes" knob ADR 0001 rejected. A Log
Record is evidence that already happened; a failed probe is Bugler's own observation and can
simply be wrong. The count compensates for the observer, not for sensitivity, and it gates only
opening — once an Episode is open its Alert has gone out and every failure is a Match on the spot.
Recovery runs through the **ordinary Quiet Window**: a crash-looping container that dies and
returns ten times in five minutes is one Episode and one Alert, which is exactly what ADR 0001
built Episodes to do, and the reserved Fingerprint means ADR 0004's per-kind override already
tunes health check Episodes separately from log ones, with no new setting.

**No guard against Bugler's own blindness.** If Bugler loses DNS, every watched Service fails at
once and opens an Episode each. Suppressing "everything failed simultaneously" was considered and
rejected: it would filter out precisely the total outage one most wants to hear about, and since
silence is not detected there would be no second net under it. The damage is bounded — one Alert
per Service, self-clearing through the Quiet Window when the network returns — and "Bugler cannot
reach ten of your ten Services" is worth saying whoever is at fault.

## Consequences

- Alerting opens outbound connections. It is the only context that does, and the only direction in
  Bugler where a Service is contacted rather than heard from (ADR 0006). The address lives in
  Alerting's settings, not in Registry: it exists to serve the watch and has no other reader, and
  putting it in Registry would change what a Service *is* in the context that defines it.
- Detection latency for death is the loop beat times three, under a minute at the default. Bought
  knowingly against one dropped packet mailing everybody.
- The tally of consecutive failures is in memory. A restart mid-outage delays the Episode by one
  more round of probes and loses nothing else; persisting it would mean a write per Service per
  beat to record that all is well.
- Saving an address probes it once and reports what came back, so a typo shows itself immediately
  instead of at 3am. It cannot catch an address that answers 200 but belongs to something else.
- A slow endpoint cannot stall the loop: probes run concurrently with a five-second timeout, and
  the sweep runs before closing so a failure counts as a Match before any Quiet Window is measured.
