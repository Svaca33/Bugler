import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";

/** What one bulk hand came to, in numbers and in the server's own words. */
export interface SelectionOutcome {
  filed: number;
  refused: number;
  /** The refusal sentences, deduplicated and joined — the server spoke them, the bar repeats them. */
  reasons: string;
}

/**
 * The bar above the list while Episodes are selected: how many, and the one hand the
 * selection offers — the same Archived mark laid on all of them. It stays up after the hand to
 * say what was filed and what was refused, because a selection with an open Episode in it must
 * never half-succeed in silence; with nothing selected and nothing to report, it is not there.
 */
export function SelectionBar(props: {
  count: number;
  pending: boolean;
  outcome: SelectionOutcome | undefined;
  onArchive: () => void;
  onClear: () => void;
  onDismiss: () => void;
}) {
  const t = useT();
  const { count, outcome } = props;
  if (count === 0 && outcome === undefined) return null;

  const words = t.alerting.selection;
  return (
    <div
      data-testid="selection-bar"
      className="flex min-h-[38px] flex-wrap items-center gap-3 border-b border-[#17293D] bg-[#0F1F33] px-5 py-1.5"
    >
      {count > 0 && (
        <>
          <span className="font-mono text-[11px] whitespace-nowrap text-[#DCE8F3]">
            {words.selected(count)}
          </span>
          <Button size="sm" disabled={props.pending} onClick={props.onArchive}>
            {props.pending ? t.common.loading : words.archiveSelected}
          </Button>
          <Button variant="ghost" size="sm" disabled={props.pending} onClick={props.onClear}>
            {words.clear}
          </Button>
        </>
      )}
      {outcome !== undefined && (
        <>
          <span
            role="status"
            className={`text-[12px] ${outcome.refused > 0 ? "text-severity-warn" : "text-[#8CA1B8]"}`}
          >
            {outcome.refused === 0
              ? words.filed(outcome.filed)
              : words.filedAndRefused(outcome.filed, outcome.refused, outcome.reasons)}
          </span>
          <Button variant="ghost" size="sm" onClick={props.onDismiss}>
            {words.dismiss}
          </Button>
        </>
      )}
    </div>
  );
}
