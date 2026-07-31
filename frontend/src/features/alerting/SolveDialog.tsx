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
  const lastMatchAgo = describeMillis(Date.now() - Date.parse(episode.lastMatchAt));

  return (
    <Dialog open={props.open} onOpenChange={props.onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Solve this Episode?</DialogTitle>
          <DialogDescription>
            Solved is the verdict that the cause was fixed. It is final — there is no unsolve.
            {episode.state === "Open" && (
              <>
                {" "}This Episode is still open (last match {lastMatchAgo} ago): solving closes
                it on the spot, and a later matching log opens a new Episode with a new Alert.
              </>
            )}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="ghost" onClick={() => props.onOpenChange(false)}>
            Cancel
          </Button>
          <Button disabled={props.pending} onClick={props.onSolve}>
            Solve
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
