---
status: accepted
---

# A Focus narrows the view, not the reading

A **Focus** is a User's own choice of which Applications they attend to. It is resolved against
their Visibility Scope on every query, so it can only subtract from what they may read; it is not a
permission, nobody else sets it, and no answer is ever refused because of it. An Admin still reads
everything — they simply stop being *shown* everything.

**The problem it answers.** An Admin reads all telemetry by definition (`CONTEXT.md`), and the read
path implements that literally: `GrantedVisibility` answers `null` for an Admin. On a server a few
people share, the Admin's dashboard, log list and Episode list therefore fill with every
Application on it, including the ones a colleague tends. The Admin role costs you a usable default
view.

**Why not simply let grants bind Admins.** It would be cosmetic as security — an Admin manages
grants and could lift any bound placed on them. It would contradict the reason the role exists: a
server must always have someone who can see a misconfigured Application nobody holds a grant to.
And it would silently narrow that Admin's Machine Delegations, which lend the User's Visibility
Scope (ADR 0029) — a fixer agent watching the whole server would lose sight the moment its issuer
tidied their own dashboard. What an Admin lacks is not a smaller right but a smaller default view,
so that is what a Focus is.

**Why a second contract rather than a wider `IReadVisibility`.** `IReadVisibility` is a single
chokepoint, and putting the Focus inside it would have focused everything that asks — including the
things that must not be. `EpisodeDetailEndpoint` 404s outside the Scope; a lens that 404s a link is
a lock. `EpisodeActionEndpoints` writes; a lens that refuses a write is a lock. `SubscriptionEndpoints`
validates where a Subscription may point; a lens that stops you subscribing is a lock. And the
machine door resolves through the same call. So the Focus lives in `IReadApplicationFocus`, beside
`IReadVisibility` rather than inside it: listing endpoints ask for the lens, endpoints that
authorize one thing ask for the right, and the difference is visible in the constructor of every
call site. `tests/Bugler.ArchitectureTests` forbids `*/Mcp/**` from referencing the lens at all,
which is what keeps ADR 0029 true by construction rather than by memory.

## Considered Options

- **A client-side Focus, in the browser.** Cheapest, and wrong in three ways: every browser would
  start over, the server would keep answering with everything, and the Episodes badge would keep
  counting a server the reader is not looking at. It also does not reach mail.
- **Storing the Focus server-side but applying it in the SPA.** It buys nothing: the Focus is a
  *set* of Applications and the Source Filter addresses one (`SourceFilter.Application`), so every
  listing endpoint would have to learn a set of Applications anyway. The saving was illusory and
  the discipline cost real — one forgotten dropdown leaks an Application the reader asked not to
  see.
- **Storing the Focus as the Applications to hide rather than the ones to show.** Tempting,
  because a newly registered Application would then be born *inside* every Focus and could never
  become a silent blind spot. Rejected because Applications are registered rarely on a self-hosted
  server and the people sharing it tell each other; carrying the inverted set for that one case was
  not worth the inversion everywhere else.
- **`uuid[]` on `access.users` instead of a table.** Its one advantage was telling "no Focus" from
  "a Focus of nothing" for free. Once an empty Focus was decided to mean nothing, that advantage
  disappeared and the table won: it mirrors `ApplicationGrant`, cascades with the User, and lets
  the `ApplicationDeleted` handler delete by Application exactly as it already does for grants.

## Consequences

- **An empty Focus shows nothing, and a Focus is not seeded.** Every account — including every
  account that existed before this landed — starts with an empty Focus and an empty Bugler until
  its owner sets one. This is the reverse of what issue #41 proposed ("unset means everything") and
  it was chosen deliberately: attending to nothing is a choice a person makes, not a state Bugler
  assumes for them. Seeding the migration from the grants was rejected because it would have left
  precisely the Admin — who holds no grants — at zero, and reading `registry.applications` from an
  Access migration to fix that was a cross-schema exception bought for one upgrade.
- **The Focus is silent.** No banner, no count of what stands outside it, no way to widen a single
  view without changing the Focus. The one place that speaks is the empty state: when the resolved
  Focus is empty, the Dashboard, Logs, Traces and Episodes replace their canvas with a sentence and
  a button to the setting. Issue #41 argued for a standing indicator on the grounds that an
  observability tool must never let "hidden" read as "absent"; that argument was heard and the
  quieter design taken instead, on the grounds that the reader set the Focus themselves. The empty
  state is the concession, and it is the case where the confusion would actually bite.
- **A Focus silences mail.** A User is not mailed about an Application they are not attending to,
  even where they hold a Subscription to one of its Services. The Subscription is kept, not
  deleted: widening the Focus makes it speak again. This too reverses issue #41, and it means one
  intent is now expressible in two places that can disagree — accepted because "I am not watching
  this" is the more human of the two sentences, and because the Subscriptions panel lives on the
  Episodes page and is focused with it, so a silenced Subscription is not shown pretending to work.
  Owed mail to a User outside whose Focus the Application stands **lapses at once** rather than
  waiting out its time-to-live: "not deliverable yet" and "not wanted" are different answers, and
  delivering the second one late would mail somebody about trouble already solved.
- **Google Chat is untouched.** An Application's webhook belongs to no one person, so no one
  person's lens may quiet it.
- **The Focus stops at the machine door and at the Admin section.** A Machine Delegation lends the
  Visibility Scope, not the lens its issuer happens to hold this week (ADR 0029). And the whole
  Admin section — Topology, Storage, People, Server — speaks the full Visibility Scope, because
  otherwise an Admin who registered a new Application would watch it vanish from the list they
  created it in, and could not give it a Service or an API key without widening their own view
  first. The rule is one sentence: *a Focus governs what reaches you, never what you configure.*
- **Naming a source beats the Focus.** A query that names an `applicationId` or a `serviceId` is
  answered from the Visibility Scope, so a pasted link and a hand-written API call still work and
  nothing ever answers 403 because of a lens. The UI never offers what a Focus hides, so this
  matters only to somebody who went looking on purpose — which is the point.
- **`/api/auth/me` answers with the resolved Focus**, not the stored rows. A member whose grant is
  revoked out from under their Focus falls to an empty resolved set and gets the empty state, the
  same as somebody who chose nothing. The stored rows are left alone by that revocation: a grant
  can come back, and an Admin's action should not silently rewrite somebody else's choice.
- Nothing records that a Focus changed. Bugler keeps no audit, here as elsewhere.
