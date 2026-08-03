---
status: accepted
---

# The server speaks the requester's language

Bugler now speaks more than English (GitHub issue #3): the SPA, the alert mails and chat cards,
and the refusal sentences the API answers with are all localized, Czech first. The decisions that
shaped it, and the ones deliberately not taken:

**One axis: a Language, not a locale pair.** A supported Language ("en", "cs") carries both its
words and its formatting conventions — en renders dates as en-GB, cs as cs-CZ. Nobody chooses
words and number formats separately, and the browser's regional preference is never consulted:
what a Czech instance shows does not depend on which desk it is read from.

**Where the choice lives.** `access.users` gains a nullable language; **null means "follow the
server"**, so an admin changing the server's language in Administration → Server carries everyone
who never chose. The server's language is a single Host-owned row (ADR 0014's pattern — a
deployment fact, no context's business), read through `IServerLanguage` and defaulting to English
while unset. It is also what the screens before sign-in speak, and the language of the Google
Chat card — a webhook names a room, and a room's language is the deployment's, not any person's.
Alert mails are composed per recipient in the language `MailRecipient` carries; the reset mail
speaks the account's language, not the asker's.

**Requests carry their language; the verbatim contract survives.** The UI shows server refusals
word for word (a 409 carries the model's own sentence), so the sentence must arrive in the
language the UI is speaking. Rather than minting a language claim into the Session — which goes
stale the moment the choice changes — the SPA sends its active language as `Accept-Language` on
every call, and the server honours it when supported, falling back to the server's language.
Trusting the header is harmless: it chooses only the words of the answer to that requester. The
alternative — error codes translated by the client — was rejected because it abandons the
verbatim contract and blinds every non-SPA caller.

**Typed catalogs, no resx, no i18n framework.** Each bounded context owns an abstract
`…Messages` class (one abstract member per sentence) with one sealed class per language; the
frontend mirrors this with a `Messages` interface implemented by `i18n/en` and `i18n/cs`.
Translation is done by AI during development, which inverts the usual weights: extraction
tooling for human translators is worthless, and **compile-enforced completeness is everything** —
a language missing a sentence fails `dotnet build` or `bun run typecheck`, never falls back
silently at runtime. Adding a language is one new class per context, one new frontend directory,
and one loader arm; the compilers list everything else.

**What stays English on purpose.** Machine-facing text is not spoken in a Language at all:
`/health`, OTLP ingest answers, `ILogger` output, `Delivery.LastError`, severity band names
(ERROR is ERROR in Czech), the UTC stamps in alert mails, and query-grammar validation only a
hand-crafted URL can trigger. The boundary is "will a person read this in the UI or a message" —
not "is it a string".
