# Volume resolves its own window and its own buckets

A Volume query answers with the window it actually used and the Bucket width it actually chose, both decided server-side and both reported back in the response. The browser cannot compute either: a Relative Range is resolved against the server clock (ADR 0002), so a viewer whose clock drifts would draw an axis that disagrees with the rows underneath it — and an axis that lies about which hour it covers is worse than no axis, because the list below it looks wrong instead.

An open end of a Time Filter resolves to what the matching telemetry reaches, so a window is nameable even when the Filter bounded nothing. The lower edge is the declared bound, or the oldest matching Log Record when there is none. The upper edge is the declared `to`, or `max(now, newest matching Log Record)` — which means **the axis may extend past `now`**. That is deliberate and follows ADR 0002 to its conclusion: a Relative Range does not cap at `now` precisely so that telemetry stamped in the future by a skewed sender stays visible, and a chart that stopped at `now` would hide the very anomaly the list is built to show. Silence inside the declared window renders as empty Buckets rather than as a shorter axis, because in an observability tool a gap is the finding.

Bucket width comes from a fixed ladder — `1s, 5s, 10s, 30s, 1m, 5m, 15m, 30m, 1h, 3h, 6h, 12h, 1d, 7d, 30d` — the smallest rung yielding at most about sixty Buckets. Edges fall on multiples of that width in **UTC**, never in the viewer's zone. Aligning to the viewer would make a Bucket a calendar day rather than a fixed span, and daylight saving would then hand one Bucket of the year an extra hour of telemetry: a rendering artefact indistinguishable from a spike, twice a year, in a tool whose whole job is telling those apart. It would also make the same link render differently for two people looking at it together.

## Considered Options

- **Clip the axis at `now`** — simplest, and wrong for the same reason ADR 0002 gives: it makes future-stamped records invisible in the chart while they sit at the head of the list.
- **Draw the axis over the data extent only** — a "last hour" showing traffic in its final five minutes would render a five-minute chart, and the fifty-five silent minutes, usually the interesting part, would simply not exist.
- **A fixed number of Buckets stretched to fit the window** — edges then slide with every refetch of a Relative Range, re-binning the whole chart several times a minute under Follow. A Volume chart is read by comparing bars to each other; bars that re-bin under the reader have nothing stable to be compared against.
- **Widen the query to whole Buckets** so the edges are never cut — makes the end bars full, at the cost of counting Log Records the list does not show. Dropping the cut Buckets instead hides Log Records the list does show. Both break the one property the chart is built on: it is the same set as the list, aggregated.

## Consequences

- Cut leading and trailing Buckets are reported as they are; the client dims them and says in the tooltip whether the Bucket was cut by the window or has simply not finished elapsing. Those two are visually identical and mean opposite things.
- Because edges stand still while a Relative Range slides beneath them, Follow only ever grows the last bar.
- The ladder has to reach `30d` so that a Filter bounding nothing still terminates at a readable number of Buckets.
- The response is dense — every Bucket including the empty ones — so no client can accidentally collapse a gap by forgetting to synthesise the missing ones.
- Volume runs under its own `statement_timeout`. On a window too wide to aggregate, the chart says so and the list still answers; the alternative is a page that hangs on the cheap query because the expensive one is still running.
- Axis labels render in the viewer's local time even though Buckets are aligned in UTC, so a daily bar in Central Europe is honestly labelled `02:00` rather than pretending to be midnight.
