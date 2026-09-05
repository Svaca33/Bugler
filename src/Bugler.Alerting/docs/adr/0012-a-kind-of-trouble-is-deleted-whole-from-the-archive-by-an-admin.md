---
status: accepted
---

# A kind of trouble is deleted whole, from the archive, by an Admin

ADR 0001 made Episodes records of outages that outlive their evidence and go only with the
Deletion of their Service or Application. That kept every answer about a kind of trouble honest,
and it also meant a kind nobody would ever look at again — a misconfiguration fixed a year ago,
a test tenant's noise — sat in the record forever. Archived (the change before this one) files
such a kind out of the everyday view; this decision lets an Admin remove it from the record, and
draws three lines around how.

**The unit is the kind, never one Episode.** Several answers an Episode gives are claims about
its kind rather than about itself: how often it recurred before, whose hand was laid on it
earlier, whether a Solved Proposal or Resignation has been overtaken, which Episode is the kind's
face in the grouped list. Delete one Episode out of a surviving history and every one of those
answers changes silently — a recurrence count shrinks, an overtaken proposal becomes standing
again and re-counts in the rail badge, the grouped list hands a row to a different Episode — for
a reader who could never have seen the deleted one. Deleting the kind entire leaves nobody behind
to answer wrongly. The kind is what ADR 0011 keys it as, (Episode Scope, Watch, Fingerprint),
and the endpoint is addressed through any Episode of it, as the Quiet Window override is (ADR
0004), so no caller ever handles an opaque Fingerprint as a value.

**Only from the archive.** Deletion is refused while any Episode of the kind is open or not yet
Archived. An open Episode is still taking Matches, and deleting it would only have detection
reopen the kind on its next one with a fresh Alert; and Archived is the reversible step that the
irreversible one must be reached through — a kind can be put away and taken back out for as long
as anyone hesitates, and only what has been put away, whole, can be destroyed.

**Only by an Admin, under its own name.** Deleting a kind destroys the record of who acknowledged
and who solved it, which is closer to a Deletion in Registry than to a triage gesture, and it is
the one operation in Alerting permitted to destroy a Journal — ADR 0006 said entries die only
with their Episode, and this is the hand that kills the Episode. The endpoint asks for
`DeleteKindsOfTrouble` (ADR 0015) rather than riding on `ConfigureAlerting`, because tuning the
watch and erasing its record are different powers even while the same role holds both.

## Considered Options

- **Deleting one Episode.** Rejected above: it leaves siblings answering wrongly about their own
  kind, and the reader has no way to know an answer is short by one.
- **Letting Archived be enough — never deleting.** Rejected for the record's sake, not the view's:
  the view is already tidy. A kind nobody will read again still counts in the archive, still
  holds a Quiet Window override, still names people who may since have left. An Admin should be
  able to say it is over.
- **Deleting from any state, as Registry deletes a Service.** Rejected: a Service's Deletion is
  about a sender that is gone, and its Episodes go because there is nothing left to be about. A
  kind's Deletion is about the record alone, and an open Episode is not yet a record — it is
  still happening.
- **A soft delete or a tombstone.** Rejected as Archived by another name. The point of this hand
  is that the rows are gone; anything that keeps them is filing, and filing already exists.

## Consequences

- The whole kind goes in one transaction — Episodes, and by cascade their Participations,
  Journal entries, Readings and owed Deliveries — with the kind's Quiet Window override, which
  hangs off no Episode and is deleted explicitly. A kind whose Episodes span several Services goes
  the same way, because since ADR 0034 an Episode has no Service to be scoped by.
- Detection is untouched. Trouble that returns after its kind was deleted opens a new Episode
  with no history — which is the truth, and reads as a first occurrence.
- Because the destruction is permanent, the UI never offers it one click away: the button opens
  a guard that names what is about to be lost and stays disarmed until the word is typed back,
  the same shape Registry puts in front of deleting a Service (ADR 0007).
- The machine door does not offer it. A Machine Delegation lends a User's reading (ADR 0029) and
  the machine hand's verbs stop short of the verdict (ADR 0010); erasing the record is further
  still.
