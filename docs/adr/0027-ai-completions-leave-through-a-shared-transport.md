---
status: accepted
---

# AI completions leave through a shared transport, not through a context

Calling a language model lives in `Bugler.Ai`, a transport every context may reference — like
`Bugler.Mail` (ADR 0011), and like it, not a bounded context. It offers one seam:
`IAiCompletion`, which carries a prompt to the configured provider and returns its answer. What
the prompt asks and what the answer means are the caller's business alone; the transport owns no
data and no lifecycle. Two providers stand behind the seam from the start: the Anthropic API and
any OpenAI-compatible endpoint — the second not for OpenAI's sake but so a self-hosted operator
can point Bugler at their own Ollama or vLLM and let nothing leave the building.

Settings follow SMTP's pattern to the letter (ADR 0014): an `Ai` configuration section, a
runtime-editable form on Administration → Server whose saved row — held by Host in the `server`
schema — wins wholly over configuration until reset, an API key that is write-only through the
API, and a test button that proves the saved configuration, never the form's unsaved edits.
Unset means AI is off everywhere, exactly as unset SMTP means no mail — for a self-hosted tool,
optional from the first day is the condition of being accepted at all.

The prompt was the Reading — Alerting's explanation standing beside an Alert — but the seam is
deliberately dumber than its first caller, because the next callers (a filter written from plain
language in Exploration, similarity over Episodes) share nothing with it except the need to ask
a model something.

## Considered Options

- **A client inside Alerting, extracted when a second consumer arrives.** The YAGNI answer, and
  wrong for the same reason SMTP-in-Alerting was: the settings screen and the "is AI on at all?"
  question would belong to a context that merely happened to ask first.
- **A bounded context.** The full ceremony — CONTEXT.md, schema, arch tests — for a node that
  holds no domain data and no lifecycle. If AI ever accrues its own state (budgets, generation
  history), promote it then.
- **Official SDKs (Anthropic C#, OpenAI .NET).** Two dependencies and two API surfaces for what
  is one POST each (`/v1/messages`, `/v1/chat/completions`). Hand-rolled `HttpClient` keeps
  timeouts, errors and the OpenAI-compatible quirks of local servers in our own hands.
- **A richer seam — streaming, tool calls, structured output.** Rejected: prompt in, text out. A
  caller who needs structure asks for it in the prompt and parses the answer; the seam grows the
  day a caller genuinely cannot.

## Consequences

- The architecture tests admit `Bugler.Ai` to every context, on the same terms as `Bugler.Mail`
  and SharedKernel.
- The transport asks `IAiSettingsSource` at every call, so a save on the Server screen applies
  to the very next completion without a restart — and whether AI "is on" is answered by the
  source, never by bound options (ADR 0014's lesson).
- Every completion has a deadline; a hung provider fails that call and never the caller's loop.
- Callers must degrade to silence when the source says AI is unset — the features are ornaments
  on Bugler, never load-bearing.
