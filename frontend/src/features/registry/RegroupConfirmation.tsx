import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useT } from "@/i18n";

/**
 * The guard on a re-partition: changing the Fingerprint Rule or the Episode Scope leaves every
 * open Logs Episode in a partition nothing will report again, so saving Mutes them and drops the
 * quiet windows keyed on the old Fingerprints (ADR 0033, 0034).
 *
 * It is not a deletion, so it does not ask for a phrase to be typed back the way ADR 0007's guard
 * does — but it is irreversible and it is measured in episodes somebody is currently working, so
 * it says how many before it lets the button be pressed.
 */
export function RegroupConfirmation(props: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** How many open Logs Episodes the change will Mute; undefined while they are still being counted. */
  cost: { count: number; capped: boolean } | undefined;
  pending: boolean;
  failed: string | undefined;
  onConfirm: () => void;
}) {
  const t = useT();
  const words = t.registry.groupingCard;

  return (
    <Dialog open={props.open} onOpenChange={props.onOpenChange}>
      <DialogContent data-testid="regroup-warning">
        <DialogHeader>
          <DialogTitle>{words.confirmTitle}</DialogTitle>
          <DialogDescription>{words.confirmIntro}</DialogDescription>
        </DialogHeader>

        {/* The count is the whole reason this asks rather than saves, so it carries the alert's
            own weight rather than sitting in the description above. */}
        <p
          role="alert"
          className="rounded-[9px] border border-destructive/40 bg-destructive/10 px-3 py-2.5 text-[12.5px] leading-[1.5] text-foreground"
        >
          {props.cost === undefined
            ? words.warningCounting
            : words.warning(props.cost.count, props.cost.capped)}
        </p>

        {props.failed !== undefined && (
          <p className="text-[11.5px] text-destructive">{props.failed}</p>
        )}

        <DialogFooter>
          <Button type="button" variant="ghost" onClick={() => props.onOpenChange(false)}>
            {t.registry.cancel}
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={props.pending}
            onClick={props.onConfirm}
          >
            {words.confirmButton}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
