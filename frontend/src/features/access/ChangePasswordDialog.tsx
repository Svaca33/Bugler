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

import { LanguageSelect } from "./LanguageSelect";
import { useChangePassword } from "./useAuth";

/** Your account, reached from your own e-mail in the header: the language Bugler speaks to you, and a Password Change proven by the password being replaced. */
export function ChangePasswordDialog(props: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const t = useT();
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
          <DialogTitle>
            {changed ? t.access.changePassword.changedTitle : t.access.account.title}
          </DialogTitle>
          <DialogDescription>
            {changed ? t.access.changePassword.changedDescription : t.access.account.description}
          </DialogDescription>
        </DialogHeader>

        {changed ? (
          <DialogFooter>
            <Button type="button" onClick={() => close(false)}>
              {t.access.changePassword.done}
            </Button>
          </DialogFooter>
        ) : (
          <>
            <LanguageSelect />
            <form
              className="grid gap-3.5 border-t border-border pt-4"
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
                <Label htmlFor="current-password">
                  {t.access.changePassword.currentPasswordLabel}
                </Label>
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
                <Label htmlFor="new-password">{t.access.changePassword.newPasswordLabel}</Label>
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
                <Label htmlFor="confirm-password">
                  {t.access.changePassword.repeatPasswordLabel}
                </Label>
                <Input
                  id="confirm-password"
                  type="password"
                  value={confirmation}
                  onChange={event => setConfirmation(event.target.value)}
                  required
                />
              </div>

              {mismatch && (
                <p className="text-[12.5px] text-destructive">{t.access.passwordsDoNotMatch}</p>
              )}
              {change.error !== null && !mismatch && (
                <p className="text-[12.5px] text-destructive">{change.error.message}</p>
              )}

              <DialogFooter>
                <Button type="button" variant="ghost" onClick={() => close(false)}>
                  {t.access.changePassword.cancel}
                </Button>
                <Button type="submit" disabled={change.isPending || mismatch}>
                  {change.isPending
                    ? t.access.changePassword.submitting
                    : t.access.changePassword.submit}
                </Button>
              </DialogFooter>
            </form>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
