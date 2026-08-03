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

import { confirmationMatches } from "./deletionConfirmation";

/**
 * The guard on an irreversible deletion: it names exactly what is about to be lost and stays
 * disarmed until the admin types that name back (ADR 0007).
 */
export function DeleteConfirmation(props: {
  /** Identifier of what is being deleted — the phrase itself may contain spaces or slashes. */
  inputId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  consequence: string;
  phrase: string;
  pending: boolean;
  failed: boolean;
  onConfirm: () => void;
}) {
  const t = useT();
  const [typed, setTyped] = useState("");
  const armed = confirmationMatches(typed, props.phrase);

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
            <DialogTitle>{props.title}</DialogTitle>
            <DialogDescription>
              {props.consequence} {t.registry.deleteDialog.cannotBeUndone}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor={`confirm-${props.inputId}`}>
              {t.registry.deleteDialog.typeBeforePhrase}{" "}
              <code className="font-mono text-foreground">{props.phrase}</code>{" "}
              {t.registry.deleteDialog.typeAfterPhrase}
            </Label>
            <Input
              id={`confirm-${props.inputId}`}
              autoComplete="off"
              autoFocus
              value={typed}
              onChange={event => setTyped(event.target.value)}
            />
          </div>

          {props.failed && (
            <p className="text-[11.5px] text-destructive">{t.registry.deleteDialog.failed}</p>
          )}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => handleOpenChange(false)}>
              {t.registry.cancel}
            </Button>
            <Button type="submit" variant="destructive" disabled={!armed || props.pending}>
              {t.registry.delete}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
