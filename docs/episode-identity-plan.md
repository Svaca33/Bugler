# Implementation plan — Episode identity and scope

The design is settled and recorded: [ADR 0033](adr/0033-a-fingerprint-is-distilled-from-the-stack-trace.md), [ADR 0034](adr/0034-an-episode-is-bound-by-a-scope-not-by-the-service.md), and the Language section of [src/Bugler.Alerting/CONTEXT.md](../src/Bugler.Alerting/CONTEXT.md). This file is the working order for building it and dies when the work lands.

## Settled numbers

| What | Value | Where it belongs |
|---|---|---|
| Stack read cap | 32 kB — `left(…, 16384) ‖ marker ‖ right(…, 16384)` past it | detection SQL |
| Poll page | 1000 rows (was 5000) | `EpisodeDetector.PageSize` |
| Storm threshold | more than 10 Episodes opened in one Scope within 15 minutes | `AlertingOptions` |
| Storm digest | one per Scope per window | `AlertingOptions` |
| Open Episodes per Scope | **no limit** | `MaxOpenEpisodesPerService` and `Fingerprint.Overflow` are deleted |
| Participations per Episode | 50 | constant beside the Episode |
| `Title` length | 300 — the length the Fingerprint column has today, reused rather than reinvented | column constraint |
| Recipe version | starts at 1; legacy rows carry 0 | stamped on every Episode |

**Deleting a Service** no longer takes Episodes with it, because an Episode is no longer the Service's. Its Participations go, a matching `OpenedByServiceId` is nulled out, and the Episode itself is deleted only when no Participation is left — that is, only when nobody may still see anything in it.

## Phase 1 — The recipe, in isolation

Pure functions, no schema, no I/O. Everything here is unit-testable and everything after it depends on it.

- Rewrite [Fingerprint.cs](../src/Bugler.Alerting/DetectEpisodes/Fingerprint.cs) into the ladder: named attribute → stack → exception type → message. Returns the hash, the Title, and **which rung produced it** (the visible degradation — a value, not a log line).
- New `StackFrames` reader: one recipe per Runtime, chosen by `telemetry.sdk.language`.
  - `at`-family (`dotnet`, `java`, `kotlin`, `nodejs`, `webjs`) — lines matching `at …`; drop `Caused by:`, `... N more`, `--- End of …`.
  - `python` — `File "…", line N, in f`; drop the echoed source line under each frame.
  - `go` — the function line; drop the indented `file:line +0x…` under it and the `goroutine N [state]:` header.
  - `php` — `#N …: Class->method()`; keep the call, drop the path; `{closure}` and `{main}` are frames.
  - `ruby` — anchored on `:in 'method'`, **path kept** (it is the identity), rest of the line ignored.
  - Unknown or absent Runtime → try the known markers; still nothing → no frames.
  - **Zero frames means the recipe was not used** — the caller coarsens one rung. Never a half-parse.
- Normalisation applied after frames are extracted: blank every digit run, collapse runs of identical frames, drop the severed line either side of the truncation marker.
- Fix the gap that started this: the message rung must read `message_template.text` (Serilog) alongside `{OriginalFormat}` (MEL) and `event.name`.
- Tests in [FingerprintTests.cs](../tests/Bugler.Alerting.Tests/FingerprintTests.cs) over **real captured stack traces** per runtime — inner exceptions, `Caused by` chains, async frames, recursion, a truncated stack, and a stack whose runtime is unknown.

## Phase 2 — Schema and migration

One EF migration in `src/Bugler.Alerting/Migrations`.

- `Episode`: `ServiceId` → `OpenedByServiceId`; add `ScopeKey`, `Title`, `RecipeVersion`, `FingerprintRung`, `StackTruncated`, `AlertFoldedIntoStorm`.
- `ScopeKey` is one canonical materialised string (`app=<guid>|env=prod|ns=BF24`, facets in fixed order, only those the Scope carries). One column, one index, no `NULL`-distinctness question — Postgres 17 could do `NULLS NOT DISTINCT`, but four nullable columns make every query read them all. A Health Check Episode's key is its Service.
- New `Participation`: `(EpisodeId, ServiceId, Version)` key, `FirstAt`, `LastAt`, `ErrorCount`, `WarnCount`. Cascade from Episode. Ceiling 50 per Episode — past it the Matches still count on the Episode and no further Participation is opened.
- `Title` is `HasMaxLength(300)`, the same as the Fingerprint column it takes over from for legacy rows.
- Cascades in [DeletedServicesHandler.cs](../src/Bugler.Alerting/DropDeletedTargets/DeletedServicesHandler.cs): stop deleting Episodes by `ServiceId`. Delete the Service's Participations, null a matching `OpenedByServiceId`, then delete the Episodes left with no Participation at all. Still idempotent — a re-delivered event must delete nothing twice. [DeletedApplicationHandler.cs](../src/Bugler.Alerting/DropDeletedTargets/DeletedApplicationHandler.cs) is unaffected: an Application still takes everything of its own with it.
- New `ApplicationFingerprintSettings` (or columns on the existing [ApplicationAlertingSettings.cs](../src/Bugler.Alerting/Settings/ApplicationAlertingSettings.cs)): rung, attribute key, the three Scope facet flags.
- `FingerprintQuietWindow` re-keys from `(ServiceId, Fingerprint)` to `(ScopeKey, Fingerprint)`.
- Index `ix_episodes_one_open_per_kind` moves to `(ScopeKey, Watch, Fingerprint)` filtered on `closed_at IS NULL`; `ix_episodes_kind_history` to `(ScopeKey, Fingerprint)`.
- `EpisodeCloseReason.Regrouped = 4` — displays as Muted (the `State` mapping already sends everything that is not `QuietWindow` or `Solved` there), but records the truth rather than claiming a Watch was switched off.
- Data migration, in this order: Title ← old Fingerprint (it was readable — that is the whole trick); `RecipeVersion = 0`; one synthetic Participation per Episode from its Service with a null version and the Episode's counts and times; `ScopeKey` ← `service=<id>` for every legacy row; open Logs Episodes closed as `Regrouped`; every `FingerprintQuietWindow` row deleted.

## Phase 3 — Detection

- [EpisodeDetector.cs](../src/Bugler.Alerting/DetectEpisodes/EpisodeDetector.cs): poll SQL grows `attributes->>'exception.type'`, the head+tail of `attributes->>'exception.stacktrace'`, `attributes->>'message_template.text'`, `resource_attributes->>'telemetry.sdk.language'`, `resource_attributes->>'service.version'`; page size to 1000.
- [DetectionBatch.cs](../src/Bugler.Alerting/DetectEpisodes/DetectionBatch.cs): accumulate on `(ScopeKey, Fingerprint)` instead of `(ServiceId, Fingerprint)`; carry the per-Service-and-version tallies for the Participations; delete `MaxOpenEpisodesPerService` and the overflow folding entirely.
- Opening an Episode writes its Participation with it, in the same transaction as the Episode and its Deliveries. A Match from a Service and version already present touches `LastAt` and the counts; a new pair opens a Participation and **owes the joining Alerts** (Phase 5).
- The Scope facets come from the settings snapshot, resolved once per run like Sensitivity is.

## Phase 4 — Closing, windows, settings

- [EffectiveSettings.cs](../src/Bugler.Alerting/Settings/EffectiveSettings.cs) and [EpisodeCloser.cs](../src/Bugler.Alerting/CloseQuietEpisodes/EpisodeCloser.cs): Quiet Window overrides resolve on `(ScopeKey, Fingerprint)`. Sensitivity stays per Service and is still decided per row — an Episode may be fed by Services whose Sensitivities differ, and that is correct.
- [AdminAlertingEndpoints.cs](../src/Bugler.Alerting/ManageAlertingSettings/AdminAlertingEndpoints.cs): read and write the Fingerprint Rule and the Scope. Admin only, as the rest of alerting settings already are.
- Changing either **mutes the Application's open Logs Episodes as `Regrouped` and deletes its Quiet Window overrides**, in the same transaction as the settings write. The response says how many, so the UI can warn before and confirm after.

## Phase 5 — Alerts

- [AlertsOwed.cs](../src/Bugler.Alerting/Deliveries/AlertsOwed.cs): the audience is resolved per recipient, not per channel. On open — Application followers plus followers of the opening Service. On a new Participation — followers of the joining Service **who hold no Delivery for this Episode yet**.
- `DeliveryKind.Joined = 4` and `DeliveryKind.StormDigest = 5`.
- Storm: count Episodes opened per `ScopeKey` in the window; past 10 the further Alerts are not enqueued, the Episode is marked `AlertFoldedIntoStorm`, and one digest per Scope per window is owed instead.
- [MessageComposer.cs](../src/Bugler.Alerting/DeliverMessages/MessageComposer.cs) and [GoogleChatSender.cs](../src/Bugler.Alerting/DeliverMessages/GoogleChatSender.cs) grow the two new messages. The joining one says *since when*, never "opened".

## Phase 6 — Read path

- [EpisodesEndpoint.cs](../src/Bugler.Alerting/ListEpisodes/EpisodesEndpoint.cs), [EpisodeCountsEndpoint.cs](../src/Bugler.Alerting/ListEpisodes/EpisodeCountsEndpoint.cs), [EpisodeFilter.cs](../src/Bugler.Alerting/ListEpisodes/EpisodeFilter.cs): the Service/namespace/environment filters become `EXISTS` over Participations. `latestPerFingerprint` groups on `(ScopeKey, Fingerprint)`.
- [EpisodeDetailEndpoint.cs](../src/Bugler.Alerting/DescribeEpisode/EpisodeDetailEndpoint.cs): return the Participations, the Title, the rung, and the truncation and storm marks.
- [EpisodeActionEndpoints.cs](../src/Bugler.Alerting/ActOnEpisodes/EpisodeActionEndpoints.cs): "newest of its kind" and Solve's acknowledgement sweep move from `ServiceId` to `ScopeKey`.
- [AlertingTools.cs](../src/Bugler.Alerting/Mcp/AlertingTools.cs): `fingerprint` stays an opaque token — reword its description, it currently promises a readable one; `serviceId` filters through Participations. Answers carry the Title.
- Regenerate the client: `cd frontend && bun run regen-api`.

## Phase 7 — Frontend

- [EpisodesPage.tsx](../frontend/src/features/alerting/EpisodesPage.tsx): the group key is `(scopeKey, fingerprint)`; a row leads with the Title and shows which Services and versions are in it.
- [EpisodeDetailPanel.tsx](../frontend/src/features/alerting/EpisodeDetailPanel.tsx): a Participations table (Service, version, first, last, count) — the "does the new version still do it" read.
- [VersionAtOpen.tsx](../frontend/src/features/alerting/VersionAtOpen.tsx) is superseded for Logs Episodes: the Episode now states its versions instead of the browser inferring them from Releases. Keep the Release overlay on the Volume; it answers a different question.
- New admin panel for the Fingerprint Rule and the Scope, with a confirmation naming how many Episodes the change will mute.
- Marks for a coarsened rung, a truncated stack, and an Alert folded into a Storm.
- [EpisodesHelpDialog.tsx](../frontend/src/features/alerting/EpisodesHelpDialog.tsx) explains grouping and is now wrong — rewrite it.

## Phase 8 — Words

Every new string in **both** `en` and `cs` ([frontend/src/i18n](../frontend/src/i18n)), and the server sentences in [EnglishAlertingMessages.cs](../src/Bugler.Alerting/EnglishAlertingMessages.cs) and [CzechAlertingMessages.cs](../src/Bugler.Alerting/CzechAlertingMessages.cs) (ADR 0024). The compilers enforce completeness; the mail and chat wording follows the recipient's language, not the requester's.

## Phase 9 — Tests

- Unit: `FingerprintTests` (Phase 1), `DetectionBatchTests` (scope keys, participations, no cap), `EffectiveSettingsTests`, `CloseDecisionTests`, `MessageComposerTests` (joining and digest).
- Integration: `AlertingDetectionTests` — two Services of one Application on one Fingerprint land in one Episode with two Participations; two Environments do not; a Runtime with no recipe coarsens visibly. `AlertingCascadeTests` — deleting one of two participating Services leaves the Episode standing with one Participation; deleting the last one takes the Episode with it. `AlertingFingerprintQuietWindowTests` — re-keyed. `AlertingDeliveryTests` — one per recipient, the joining Alert, the Storm digest. New: the migration over seeded legacy rows.
- E2E [alerting-flow.spec.ts](../e2e/tests/alerting-flow.spec.ts): the settings panel, the mute warning, the Participations table.
- Architecture tests and `bun run arch` must stay green — nothing here crosses a context boundary, which is the point of reading the Runtime from telemetry rather than from Registry.

## Phase 10 — Landing it

- `dotnet build Bugler.slnx && dotnet test Bugler.slnx`, `bun test`, `bun run typecheck`, `bun run arch`.
- `powershell -File scripts/redeploy.ps1`, then click through on `:8080` before any commit.
- The version bump is **minor** — new settings, a changed model, a data migration. `scripts/bump-version.ps1 -Minor` refuses a dirty tree, so bump first and let the work land inside the new line.

## Watch out for

- **Poll memory.** 1000 rows × 32 kB is the worst case per page and it is real. Measure it once against production-shaped data before assuming the page size is right.
- **Truncation direction.** Head-only is wrong in both directions at once (ADR 0033). The seam handling is easy to get subtly wrong — test it.
- **The migration is one-way.** Muting open Episodes discards live acknowledgements and machine claims. Take a database backup before the first production run.
- **Recipe version 0 rows** must never be re-fingerprinted. They belong to a partition that no longer exists; leave them as history.
- **`ScopeKey` is derived, not authoritative.** It is written when the Episode opens and never recomputed — a Scope change mutes rather than rewrites.
