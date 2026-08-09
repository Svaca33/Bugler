---
status: accepted
---

# The Alert holds the door for the Reading

An Alert whose Episode owes a Reading waits for it — but only as long as the operator's
patience: a server-wide setting kept beside the AI provider's, reading "don't wait", a number
of seconds (60 by default), or "as long as it takes". The delivery sweep skips a due Alert
while its Episode's Reading is still pending and the Episode is younger than the patience; the
moment the Reading is written, fails for good, or the patience runs out, the Alert leaves with
whatever there is. A Reading finished after its Alert left is kept for the Episode's detail and
never mailed after the fact — exactly one Alert per Episode per channel stands.

The tension is real on both sides. The feature's worth is the explanation *inside* the alert —
"it began four minutes after 2.3.1 was deployed" read on a phone at 3 a.m. — not a paragraph
discovered in the UI the next morning. But the unattended watch must never hang on a third
party: a stale Alert wakes more panic than none, which is why Deliveries already carry a
time-to-live. So the door is held, and how long is the operator's call, because the right
number depends on what answers their prompts — an API across the ocean or a slow model on their
own metal.

"As long as it takes" is still bounded: generation has finite attempts and always reaches a
terminal state — written or failed — and the Delivery's time-to-live keeps the last word
regardless.

## Considered Options

- **Never wait.** No new mechanics and today's latency — but whether an Alert carries its
  Reading becomes a race against the sweep's beat, and most would leave unexplained.
- **Always wait for the outcome.** The best possible message, held hostage by the slowest
  possible provider: a down endpoint would silence alerting until retries exhaust. The one
  unacceptable trade — unless the operator chooses it with open eyes, which is what the
  configurable "as long as it takes" is.
- **A fixed 60 seconds.** Right for a hosted API, wrong for local models; the number is a fact
  about the operator's deployment, so it belongs on the Server screen with the rest of them.

## Consequences

- The sweep gains one look per due Alert — is a Reading pending, and how old is the Episode — 
  and a skipped Delivery simply stays due; nothing new is written and no state is invented.
- With patience set to "don't wait", an Alert may still carry a Reading when generation beat
  the sweep to it. That is a race won, not a rule broken.
- In a storm of Episodes the Readings queue behind one another; late ones may overrun the
  patience and their Alerts go out unexplained. Accepted — alert latency wins over completeness.
