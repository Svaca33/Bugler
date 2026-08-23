# Application Focus — implementation plan

Issue #41, *An Admin may focus their view without giving up their reading*. Settled in a design
session; this plan is the record of what was decided and the order it lands in. Delete it once it
has landed, as `docs/episode-identity-plan.md` was.

**Three decisions here contradict issue #41 as written.** They are deliberate, and ADR 0004 in
Access carries the reasoning:

| issue #41 says | we do |
|---|---|
| "Unset means everything" | An empty Focus shows **nothing**. A Focus is set before Bugler shows telemetry. |
| "Not mail or Google Chat" | A Focus **silences mail** for the Applications outside it. Google Chat is unchanged — it is not a per-User choice. |
| "Visible when active" — a banner counting the Applications outside | The Focus is **silent**. Only the empty state speaks, on the page it empties. |

## The model

**Focus** is a User's own choice of which Applications they attend to: the set their reading and
their mail are answered from. It is a set of `ApplicationId`, per User, and it is resolved against
the User's Visibility Scope at read time, so it can only subtract from what they may read.

Three rules carry the whole design:

1. **A Focus hides, it never refuses.** Outside a Focus an Application is absent from every listing,
   every filter option and every mail — but a query that names it (`applicationId`, `serviceId[]`)
   is answered from the Visibility Scope. Nothing ever answers 403 because of a Focus.
2. **A Focus governs what reaches you, never what you configure.** Views and mail are focused; the
   whole Admin section and the Focus editor itself speak the full Visibility Scope.
3. **A Focus is not a right.** It narrows neither a Machine Delegation (which lends the Visibility
   Scope itself, ADR 0029) nor an Application's Google Chat webhook, which belongs to no one person.

### Where it applies

| surface | focused? | why |
|---|---|---|
| Dashboard, Logs, Traces, Episodes | **yes** | reading |
| Episodes nav badge (`/api/episodes/counts`) | **yes** | reading |
| `/api/catalog` (default) | **yes** | it feeds the filter options; focusing it is what empties them without discipline in the SPA |
| Subscriptions panel + `/api/…/sensitivity` | **yes** | it lives on the Episodes page |
| Mail (`IMailRecipients`) | **yes** | a User is not mailed about what they are not attending to |
| `/api/catalog?scope=all` | no | serves the Focus card, the People tab, the whole Admin section, and the Machine Delegations card |
| The whole Admin section — Topology, Storage, People, Server | no | configuring the server, not reading it |
| Machine Delegations card on `/account` | no | a delegation binds to the Visibility Scope, so its narrowing is picked from all of it (ADR 0029) |
| MCP tools (`ExplorationTools`, `AlertingTools`) | no | ADR 0029 — a delegation lends the Scope |
| `EpisodeDetailEndpoint`, `EpisodeActionEndpoints`, `SubscriptionEndpoints` (writes) | no | a lens must not 404 a link or refuse a write |
| Google Chat | no | not a per-User choice |

## Backend

### Access — owns the Focus

- `src/Bugler.Access/Users/ApplicationFocus.cs` — entity mirroring `ApplicationGrant`
  (`Id`, `UserId`, `ApplicationId`, `CreatedAt`), unique on `(UserId, ApplicationId)`, cascade on
  the User. A join table rather than a `uuid[]` column: "empty" needs no marker now that empty
  means nothing, and the `ApplicationDeleted` handler then deletes by `ApplicationId` exactly as it
  does for grants.
- `AccessDbContext` — `DbSet<ApplicationFocus> ApplicationFocuses`, mapping beside
  `ApplicationGrant`. Migration `AddApplicationFocus`. **No data seeding** — every existing account
  starts with an empty Focus and sets one; the empty state explains it.
- `src/Bugler.Access/Contracts/IReadApplicationFocus.cs` — a second contract *beside*
  `IReadVisibility`, never inside it:

  ```csharp
  /// Which Applications the current caller is attending to: their Focus resolved against
  /// their Visibility Scope. Null means unrestricted — a caller who holds no Focus at all,
  /// which is every caller that is not a signed-in person. An empty set means nothing is shown.
  public interface IReadApplicationFocus
  {
      ValueTask<IReadOnlyCollection<ApplicationId>?> GetFocusedApplicationsAsync(CancellationToken ct);
  }
  ```

  `FocusedVisibility` implements it: resolve `IReadVisibility` first, then intersect with the
  caller's Focus rows. A Machine Delegation caller and an anonymous caller get the Visibility Scope
  back unchanged — they hold no Focus.
- `src/Bugler.Access/ManageApplicationFocus/ApplicationFocusEndpoints.cs`
  - `PUT /api/auth/focus/{applicationId:guid}` — add, idempotent
  - `DELETE /api/auth/focus/{applicationId:guid}` — remove, idempotent

  One row per click, like the grant checkboxes on the People tab. Both refuse an Application
  outside the caller's Visibility Scope (`404`), so a Focus can never name what its holder may not
  read.
- `CurrentUserDto` gains `FocusedApplicationIds` — **the resolved set** (`Focus ∩ Visibility
  Scope`), not the raw rows. That is what makes the empty state fire when a member's grant is
  revoked out from under their Focus, not only when they chose nothing.
- `RevokeDeletedApplicationGrants/DeletedApplicationGrantRevoker` also deletes the
  `ApplicationFocus` rows for the deleted Application. Rename nothing — the folder is the handler
  for `ApplicationDeleted` in Access.
- `MailRecipientResolver` — a User outside whose Focus the Application stands is **not
  deliverable and never will be**. `MailRecipientsResult` gains a third list beside `Deliverable`
  and `UnknownUserIds` (`OutsideFocus`), because "not now" and "not wanted" are different answers.

### Alerting — the mail path

- `DeliveryRunner.ResolveRecipientsAsync` collects `OutsideFocus` alongside `Unknown`, and lapses
  those Deliveries **immediately** with a reason in `LastError`, without the stale-delivery
  `LogWarning`. Holding them until the TTL would eventually deliver mail about trouble that has
  since been solved.

### Swapping the contract at the call sites

`IReadVisibility` → `IReadApplicationFocus` in exactly these, and nowhere else:

- Exploration: `ScopeResolver`, `CatalogEndpoint` (unless `scope=all`)
- Alerting: `EpisodesEndpoint`, `EpisodeCountsEndpoint`, `EpisodesByServiceEndpoint`,
  `EffectiveSensitivityEndpoint`, `SubscriptionEndpoints.List`

`SubscriptionEndpoints`' *write* validation keeps `IReadVisibility` — subscribing is configuring.

`CatalogEndpoint` gains `scope=focus|all`, default `focus`.

### Two things the implementation turned up

- **`SubscriptionEndpoints.SetOwn` replaces the caller's whole set**, and the panel it hears from is
  drawn from the focused catalog. Silence about a Subscription outside the Focus therefore had to
  stop meaning "unticked" — it was never offered. `WasOnOffer` decides that, so a Subscription
  outside the Focus is kept and speaks again when the Focus widens.
- **`CatalogEndpoint.Handle` is called straight from `ExplorationTools`.** Split into `Handle`
  (which decides) and `Compose` (which shapes), so the machine door reaches the same answer through
  the Visibility Scope without the architecture test's forbidden type ever entering an `Mcp` file.

### Naming a source beats the Focus

In `ScopeResolver.NameServiceIdsAsync` the Focus replaces the visibility set **only while the
Application facet is open**; a named `filter.Application` is resolved against the Visibility Scope
instead. Same in `EpisodeFilter.Apply` for `applicationId` and `serviceId[]`. This is what keeps
rule 1 structural rather than remembered.

## Frontend

- `frontend/src/features/access/ApplicationFocusCard.tsx` on `/account`, beside Language, Password
  and Machine Delegations. A checkbox per Application from `useCatalog({ scope: "all" })`, saving
  on change. When nothing is ticked the card itself says so plainly — that is where the choice is
  made, so that is where it is explained.
- `UsersAdminPage` switches its `useCatalog()` to `scope: "all"`, or the Admin's own Focus would
  eat the grant columns.
- A shared empty state — `FocusEmptyState` — filling the canvas of **Dashboard, Logs, Traces and
  Episodes** whenever `focusedApplicationIds` is empty: one sentence and a button to `/account`.
  It replaces the content, never sits above an empty table, and it never appears when a Focus
  exists and merely a filter found nothing. Those are two different emptinesses.
  **Rendered by the routes, not the feature pages** — dependency-cruiser's `features-are-isolated`
  forbids `features/explore` from importing `features/access`, and a route is the repo's own place
  for two contexts to meet (see the comment in `_app.admin.tsx`).
- Nothing else. No banner, no count, no `focus=off`: the Focus is silent.
- i18n: new keys in `frontend/src/i18n/sections/…` for **both** en and cs (ADR 0024).

## Docs

- `src/Bugler.Access/CONTEXT.md` — the **Focus** entry (drafted in the session; `_Avoid_: scope,
  filter, view, watchlist, preference`), and a sentence in **Admin** that the role still reads
  everything and that a Focus is the Admin's own doing.
- `CONTEXT-MAP.md` — *Exploration → Access* and *Alerting → Access* now also ask for the Focus;
  the machine door explicitly does not.
- `src/Bugler.Access/docs/adr/0004-a-focus-narrows-the-view-not-the-reading.md` — why a Focus is
  not a grant on an Admin, why an empty Focus shows nothing, why it silences mail while
  Subscriptions stay per-Application, and why it stops at the machine door.

## Tests

- `tests/Bugler.ArchitectureTests/ContextBoundaryTests.cs` — **`*/Mcp/**` must not reference
  `IReadApplicationFocus`.** This is the one rule that keeps a Machine Delegation out of its
  issuer's lens by construction rather than by memory.
- Access unit tests: `FocusedVisibility` intersects and never widens; a machine-delegation caller
  and an anonymous caller are unaffected; endpoints refuse an Application outside the Scope.
- `ScopeResolverTests`: a named Application outside the Focus is still resolved; an open facet is
  focused.
- Integration: an empty Focus answers empty lists, never 403; a named `applicationId` outside the
  Focus answers rows; a mail owed to a User whose Focus excludes the Application lapses at once.
- Frontend: `ApplicationFocusCard.test.tsx` — the card offers the whole Visibility Scope, says so
  when nothing is ticked, and keeps "no application registered" as a separate sentence.
- e2e: `registerApplication` now ends by watching the new application through the card, because a
  newly registered one is inside nobody's Focus and nothing it sends would otherwise appear.
- `BuglerHarness.AttendToEverythingAsync` keeps every User watching every Application, refreshed
  wherever either set grows. Without it every suite would answer empty — which is exactly what a
  real upgrade does until somebody sets a Focus.

## Order

1. Access: entity, migration, `IReadApplicationFocus`, endpoints, `CurrentUserDto`, deletion
   handler. Tests.
2. Swap the contract at the read call sites; `scope=all` on the catalog; naming-beats-focus in
   `ScopeResolver` and `EpisodeFilter`. Tests.
3. Mail: `MailRecipientResolver` + `DeliveryRunner`. Tests.
4. Frontend: regenerate the OpenAPI client, the card, `UsersAdminPage`, the empty state, i18n.
5. Docs + ADR + architecture test.
6. `scripts/redeploy.ps1`, then click through on :8080 before any commit of the work itself.

Version line 0.25 was opened for this (`Start 0.25`, minor — it changes the default experience and
the `/api/auth/me` contract).
