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

import { useChangePassword } from "./useAuth";

/** A Password Change: proven by the password being replaced, reached from your own e-mail in the header. */
export function ChangePasswordDialog(props: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const change = useChangePassword();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [changed, setChanged] = useState(false);

  const mismatch = confirmation.length > 0 && newPassword !== confirmation;

  const close = (open: boolean) => {
    if (!open) {
      setCurrentPassword("");
      setNewPassword("");
      setConfirmation("");
      setChanged(false);
      change.reset();
    }
    props.onOpenChange(open);
  };

  return (
    <Dialog open={props.open} onOpenChange={close}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{changed ? "Password changed" : "Change password"}</DialogTitle>
          <DialogDescription>
            {changed
              ? "Everywhere else you were signed in has been signed out. This browser stays."
              : "Your current password proves it is you. Signing in elsewhere will need the new one."}
          </DialogDescription>
        </DialogHeader>

        {changed ? (
          <DialogFooter>
            <Button type="button" onClick={() => close(false)}>
              Done
            </Button>
          </DialogFooter>
        ) : (
          <form
            className="grid gap-3.5"
            onSubmit={event => {
              event.preventDefault();
              if (mismatch) return;
              change.mutate(
                { currentPassword, newPassword },
                { onSuccess: () => setChanged(true) },
              );
            }}
          >
            <div className="grid gap-1.5">
              <Label htmlFor="current-password">Current password</Label>
              <Input
                id="current-password"
                type="password"
                value={currentPassword}
                onChange={event => setCurrentPassword(event.target.value)}
                autoFocus
                required
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="new-password">New password (min 8 characters)</Label>
              <Input
                id="new-password"
                type="password"
                value={newPassword}
                onChange={event => setNewPassword(event.target.value)}
                minLength={8}
                required
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="confirm-password">Repeat new password</Label>
              <Input
                id="confirm-password"
                type="password"
                value={confirmation}
                onChange={event => setConfirmation(event.target.value)}
                required
              />
            </div>

            {mismatch && <p className="text-[12.5px] text-destructive">The two do not match.</p>}
            {change.error !== null && !mismatch && (
              <p className="text-[12.5px] text-destructive">{change.error.message}</p>
            )}

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={() => close(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={change.isPending || mismatch}>
                {change.isPending ? "Changing…" : "Change password"}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
