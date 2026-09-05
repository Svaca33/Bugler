/**
 * The same mark laid on many Episodes at once (Alerting CONTEXT.md: Archived) — no new domain
 * concept, so no new endpoint either: each Episode is filed through its own hand and writes its
 * own Journal entry, and the outcome keeps the two piles apart. A selection with an open Episode
 * in it never half-succeeds in silence: the closed ones are filed, the open ones refused with
 * the server's own sentence, and the reader is told both.
 */

export interface BulkArchiveOutcome {
  /** Filed, in selection order. */
  filed: string[];
  /** Not filed, each with the sentence the server (or the wire) refused it with. */
  refused: { id: string; reason: string }[];
}

/**
 * Lays the hand on every id, in order and one after another: the hands are independent, the
 * refusals are per Episode, and a burst of parallel writes on one table buys nothing here.
 */
export async function archiveMany(
  ids: readonly string[],
  archiveOne: (id: string) => Promise<void>,
): Promise<BulkArchiveOutcome> {
  const outcome: BulkArchiveOutcome = { filed: [], refused: [] };
  for (const id of ids) {
    try {
      await archiveOne(id);
      outcome.filed.push(id);
    } catch (error) {
      outcome.refused.push({ id, reason: error instanceof Error ? error.message : "" });
    }
  }
  return outcome;
}

/** An Episode already filed has nothing to gain from the selection; an open one may join it and
 * be refused, because the refusal is the server's to say. */
export function isSelectable(episode: { archivedAt: string | null }): boolean {
  return episode.archivedAt === null;
}
