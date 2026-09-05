import { useState } from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useT } from "@/i18n";

/**
 * The guard on the Deletion of a kind of trouble (Alerting CONTEXT.md: Deletion). Permanent and
 * reaching every Episode of the kind, so it is never one click away: the dialog names what is
 * about to be lost and stays disarmed until the Admin types the phrase back — the same shape the
 * Registry puts in front of deleting a Service.
 */
export function DeleteKindDialog(props: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** How many Episodes the kind holds — what the sentence names as about to be lost. */
  episodeCount: number;
  pending: boolean;
  /** The server's own sentence when it refused, shown verbatim; null while nothing went wrong. */
  failure: Error | null;
  onConfirm: () => void;
}) {
  const t = useT();
  const words = t.alerting.deleteKind;
  const [typed, setTyped] = useState("");
  // Surrounding whitespace is forgiven; nothing else is — the point is to have said the word.
  const armed = typed.trim() === words.phrase;

  // Closing disarms the dialog, so re-opening never starts already confirmed.
  const handleOpenChange = (open: boolean) => {
    if (!open) setTyped("");
    props.onOpenChange(open);
  };

  return (
    <Dialog open={props.open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <form
          className="grid gap-4"
          onSubmit={event => {
            event.preventDefault();
            if (armed && !props.pending) props.onConfirm();
          }}
        >
          <DialogHeader>
            <DialogTitle>{words.title}</DialogTitle>
            <DialogDescription>
              {words.consequence(props.episodeCount)} {words.cannotBeUndone}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor="confirm-delete-kind">
              {words.typeBeforePhrase}{" "}
              <code className="font-mono text-foreground">{words.phrase}</code>
              {words.typeAfterPhrase !== "" && <>{" "}{words.typeAfterPhrase}</>}
            </Label>
            <Input
              id="confirm-delete-kind"
              autoComplete="off"
              autoFocus
              value={typed}
              onChange={event => setTyped(event.target.value)}
            />
          </div>

          {props.failure !== null && (
            <p className="text-[11.5px] text-destructive">{props.failure.message}</p>
          )}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => handleOpenChange(false)}>
              {words.cancel}
            </Button>
            <Button type="submit" variant="destructive" disabled={!armed || props.pending}>
              {words.confirm}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
