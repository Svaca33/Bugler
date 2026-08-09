import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SettingsCard } from "@/components/ui/settings-layout";
import { useT } from "@/i18n";

import { useChangePassword } from "./useAuth";

/**
 * A Password Change, proven by the password being replaced. What it costs is said before it is
 * done and confirmed after: every other Session of this account ends, this browser stays.
 */
export function PasswordCard() {
  const t = useT();
  const change = useChangePassword();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [changed, setChanged] = useState(false);

  const mismatch = confirmation.length > 0 && newPassword !== confirmation;

  return (
    <SettingsCard caption={t.access.changePassword.title}>
      {changed ? (
        <div className="flex flex-col gap-1.5">
          <p className="text-[13px] text-[#6FBF8B]">{t.access.changePassword.changedTitle}</p>
          <p className="text-[12.5px] text-[#8CA1B8]">
            {t.access.changePassword.changedDescription}
          </p>
        </div>
      ) : (
        <>
          <p className="text-[12.5px] text-[#8CA1B8]">{t.access.changePassword.description}</p>

          <form
            className="grid gap-3.5"
            onSubmit={event => {
              event.preventDefault();
              if (mismatch) return;
              change.mutate(
                { currentPassword, newPassword },
                {
                  onSuccess: () => {
                    setChanged(true);
                    setCurrentPassword("");
                    setNewPassword("");
                    setConfirmation("");
                  },
                },
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
                autoComplete="current-password"
                value={currentPassword}
                onChange={event => setCurrentPassword(event.target.value)}
                required
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="new-password">{t.access.changePassword.newPasswordLabel}</Label>
              <Input
                id="new-password"
                type="password"
                autoComplete="new-password"
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
                autoComplete="new-password"
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

            <div>
              <Button type="submit" disabled={change.isPending || mismatch}>
                {change.isPending
                  ? t.access.changePassword.submitting
                  : t.access.changePassword.submit}
              </Button>
            </div>
          </form>
        </>
      )}
    </SettingsCard>
  );
}
