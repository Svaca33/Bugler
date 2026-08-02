---
status: accepted
---

# A followed list is a window, not an answer

Follow shows matching Log Records as they are stored, and a tick carries at most one page of them. Above that rate the newest are shown and the rest are passed over — **and nothing on screen says so**: no count of what arrived, no marker where records are missing, no badge for what is waiting above the scroll. The list under Follow is a window on what is arriving, not an account of it.

This is the opposite of what every comparable tool does, so it is worth saying why. A followed list that announces "2 900 records not shown" turns the calm view into an alarm exactly when the traffic is at its worst, and a count that reads zero the rest of the time is chrome earning nothing. The reason a reader turns Follow on — a deploy going out, a fix being verified — is to see the last hundred lines, which is precisely what the ceiling keeps showing them. What Bugler will not do is show a hole and stay quiet about it *while claiming to be a query result*, which is where the rest of this decision comes from.

## Consequences

- **Leaving Follow discards the list.** What replaces it is an ordinary first page of the same Filter, contiguous as far as it goes. Without this the silence would outlive the mode: a reader would be left holding a list that looks pageable and complete, and would reason about the quiet stretch in the middle of it. The record being read is not lost with it — the detail panel fetches by id and survives.
- **`Load older` is gone while Follow is on**, for the same reason: below the oldest record Follow was handed there is nothing to carry on from.
- **The ceiling is the `LIMIT`, not a rule of its own.** A tick is the list's own query read forward from a cursor, ordered newest first, so the page size caps the burst without anything having to count it.
- **The cursor is `(timestamp, id)` and `timestamp` is the sender's stamp**, so a Log Record that reaches Bugler after the cursor was taken but is stamped before it will never appear in the followed list. That is the same silence from the other end, and the same answer: the ordinary query still finds it.
- **Volume is the one honest measure of size left on the page**, which is why it stays a server-side aggregate rather than being tallied from the records the tail happened to carry. It follows Follow's tick rather than a timer of its own, and no faster than once every five seconds — it genuinely counts every Log Record in the window.
- **The Volume response reports `now`.** Reporting the window alone was not enough to mark the Bucket that is still filling: the top of the window is stretched to cover the newest record, so that Bucket closes exactly on `to` and read as finished. [ADR 0003](0003-volume-resolves-its-own-window-and-buckets.md) promised the client would tell a cut Bucket from an elapsing one; under Follow that promise was never kept until the server said where its own clock was.

## Considered options

- **Server-sent events, woken by `LISTEN`/`NOTIFY` from Ingestion.** Genuinely pushed, sub-second, and idle-free — and it was the plan for a while. It was dropped because the value in Follow turned out to be entirely in what the list does (reads forward, stays put under the reader, keeps a bounded amount of itself) and none of it in how the records get there. Polling `/api/logs` with an `after` cursor delivers all of that with no new transport, no long-lived connections, no channel name shared across a context boundary, and one fewer thing to be down. It is the option to revisit if latency below two seconds ever matters.
- **An honest gap marker** — the newest records plus a visible "N not shown" divider. Rejected above; the divider was judged worse than the silence, given that leaving Follow now produces a clean list rather than preserving a punctured one.
- **Counts only above the ceiling** — freeze the list and report arrivals as numbers. This makes the list stop being a list at the moment it is most wanted.
- **Pausing Follow when the reader scrolls away.** Rejected: it would make the scrollbar a hidden control over the mode, and a reader returning to the top could not tell a paused stream from a quiet one.
