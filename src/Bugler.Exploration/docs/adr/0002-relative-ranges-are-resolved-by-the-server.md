# Relative Ranges are resolved by the server and left open at the top

A Relative Range travels as a symbol — `range=PT1H` — and the server turns it into `timestamp >= now() - interval` at query time, rather than the browser computing `from` and sending an instant. Bugler is self-hosted and its users' machines drift; a browser ten minutes ahead would turn "last 5 minutes" into a window starting in the future and return nothing at all, which in an observability tool reads as "ingest is broken" rather than "your clock is wrong". The server clock is the one shared reference every viewer already agrees on, and the symbolic form also survives being shared: a link says *last hour*, so the recipient sees their own last hour instead of a window that has silently gone stale. Grafana and Kibana carry relative time the same way.

The range only lower-bounds the query; there is deliberately no `AND timestamp <= now()`. Log Record timestamps come from the instrumented application's clock (`OtlpLogMapper` prefers `TimeUnixNano`), so a sender whose clock ran ahead produces records stamped in the future. Capping the window at "now" would make those records invisible in every relative window, with nothing anywhere to say so — the only way to find them would be to guess and ask for a range reaching into the future. Leaving the top open puts them at the head of the list, where the anomaly can be seen and chased.

## Consequences

- A Relative Range and an Absolute Range are mutually exclusive, and an absolute one whose ends are reversed is refused: an impossible window answers 400, never an empty page.
- The window is re-resolved on every request, so paging to older records lifts the floor by however long the paging took. At most a few seconds of records at the very bottom of the window are lost — records that were about to fall out of it anyway.
- The bounds of an Absolute Range must carry a UTC offset. A bare `2026-07-28T14:12:00` would be read as UTC and quietly shift the window by the writer's own offset, so it is refused instead.
