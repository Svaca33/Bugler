import type { Episode } from "@/api/client";
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
import { describeMillis } from "@/lib/duration";

/** The one confirmation in Alerting: Solved is final, and solving an open Episode closes it. */
export function SolveDialog(props: {
  episode: Episode;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pending: boolean;
  onSolve: () => void;
}) {
  const { episode } = props;
  const t = useT();
  const lastMatchAgo = describeMillis(Date.now() - Date.parse(episode.lastMatchAt));

  return (
    <Dialog open={props.open} onOpenChange={props.onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t.alerting.solve.title}</DialogTitle>
          <DialogDescription>
            {t.alerting.solve.verdict}
            {episode.state === "Open" && <>{" "}{t.alerting.solve.stillOpen(lastMatchAgo)}</>}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="ghost" onClick={() => props.onOpenChange(false)}>
            {t.alerting.solve.cancel}
          </Button>
          <Button disabled={props.pending} onClick={props.onSolve}>
            {t.alerting.actions.solve}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
