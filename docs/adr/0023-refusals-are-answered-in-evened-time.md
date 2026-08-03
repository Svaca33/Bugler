---
status: accepted
---

# Refusals are answered in evened time

The sign-in door and the reset door now release their anonymous answers only when a fixed floor of
time — 300 ms — has passed since the handler began: every `401` from `/api/auth/login`, and the one
sentence `/api/auth/password/forgot` says to everybody. The handler notes when it started, does
whatever the request turns out to cost, and waits out the remainder before answering. The floor is
a constant, not a measurement; the waiting is an async timer, not a thread; and the fast answers —
the `429` of a spent Attempt Budget, the `404` of a server that cannot mail, and success — are left
alone, because none of them depends on whether an account exists.

**What was leaking.** Every other channel had already been closed on purpose: `forgot` says one
sentence whoever asks, and the Attempt Budget is spent for every address including addresses
belonging to nobody, so a `429` says nothing either (ADR 0021). But the clock still answered. An
address no account uses was refused after one indexed lookup; an address an account uses was
refused after a PBKDF2 verify — deliberate work, tens of milliseconds, comfortably above the jitter
between caller and server. A deactivated account skipped the verify and answered fast again, which
told a holder of valid credentials the account stands but is switched off. And `forgot` did
round-trips for a real address — issue a ticket, queue a mail — that finding nobody never bought,
with a third timing for a ticket already standing. GitHub issue #28 is the write-up.

**Why not the textbook fix.** Verifying against a dummy hash when no User is found makes every
answer cost the same by making every answer cost a PBKDF2. Here that trade points the wrong way.
The Attempt Budget is keyed per address, so ten thousand invented addresses each get a budget of
their own — and invented addresses are exactly the traffic an attacker produces most cheaply.
Today each costs the server an indexed lookup; under a dummy verify each would cost two orders of
magnitude more, and bounding that would need the global ceiling ADR 0021 deliberately refused,
the one shape that lets a single caller close the sign-in page for everybody. Evening the clock
instead keeps today's cost profile: an invented address still buys a lookup and a timer.

**What the floor is set against.** Identity's default hasher is PBKDF2 at 100k iterations — tens
of milliseconds on server hardware — so 300 ms covers the slowest honest path with room to spare,
and sits below what a person retyping their password would notice. It is fixed rather than
calibrated at startup because a floor that moved with the hardware would be noisy to measure, one
more moving part to test, and itself something to observe across restarts.

**What remains, said plainly.** A tail: under enough load a real verify can outgrow the floor, and
on hardware slow enough it always would. A statistician could read that tail — but reading a
distribution takes samples, and samples of one address are priced by its Attempt Budget at one per
thirty seconds after the burst. The oracle closes from "one measurement answers" to "hours of
patience per address against a limiter built to notice patience". And the held connection is not
the trap ADR 0021 refused when it rejected delaying refused requests: that delay would have
replaced the `429` and made sleeping connections the attacker's normal path, where this one leaves
the `429` fast and holds a failed sign-in for at most 300 ms — keeping thousands of those open at
once is an HTTP flood, which is the proxy's business.

## Considered Options

- **Accept the leak.** An attacker wanting a list of a company's addresses usually has cheaper
  sources than a stopwatch. Rejected because the posture is already bought and paid for everywhere
  else — one sentence on `forgot`, budgets for addresses belonging to nobody — and a door that
  answers to timing makes those refusals theatre.
- **Verify against a dummy hash.** The textbook answer. Rejected above: it converts the attacker's
  cheapest traffic into the server's dearest work.
- **A dummy verify behind a global bound.** Bounds the amplification by re-introducing the global
  ceiling ADR 0021 refused — and once the bound is spent, the oracle reopens, so the complexity
  buys a part-time answer.
- **A floor calibrated at startup.** Measured once, multiplied for margin. Rejected for the moving
  part: the measurement is noisy where it matters (JIT, cold caches), the tests inherit the noise,
  and the constant serves every machine the tail argument already covers.

## Consequences

- A failed sign-in and every reset ask now take 300 ms. People meet the first rarely and the
  second almost never; scripts meet them as often as the Attempt Budget lets them.
- The evened paths hold their connection for up to 300 ms. The budgets bound how many such holds
  one address can order; raw connection volume stays the proxy's business (ADR 0021).
- Deactivation is no longer distinguishable from a wrong password by any anonymous means — timing
  included. Its `401` skips the verify and the evening covers the difference.
- `EvenedAnswer.Floor` is the one constant, and the integration tests assert only lower bounds
  against it: that an answer is *not* slowed is visible in the handlers, and an upper bound
  measured on CI would flake.
- Hardware where a verify routinely exceeds 300 ms reopens a sliver of the channel rather than
  breaking anything; the budget still prices the samples.
