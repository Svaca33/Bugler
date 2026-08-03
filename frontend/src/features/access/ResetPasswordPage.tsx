import { Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { useT } from "@/i18n";

import { AuthCard, Field } from "./AuthCard";
import { useResetPassword } from "./useAuth";

/**
 * Where the link from the mail lands. The secret rides in the query string and is only ever sent
 * back in a request body — a click from a mail is a GET, and nobody has typed a password yet.
 */
export function ResetPasswordPage(props: { token: string }) {
  const t = useT();
  const reset = useResetPassword();
  const navigate = useNavigate();
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");

  const mismatch = confirmation.length > 0 && newPassword !== confirmation;

  if (props.token.length === 0) {
    return (
      <AuthCard
        title={t.access.reset.missingToken.title}
        description={t.access.reset.missingToken.description}
        footer={
          <Link
            to="/forgot-password"
            className="text-center text-[12.5px] text-[#8CA1B8] underline-offset-2 hover:text-[#DCE8F3] hover:underline"
          >
            {t.access.reset.missingToken.askForLink}
          </Link>
        }
      />
    );
  }

  return (
    <AuthCard
      title={t.access.reset.title}
      description={t.access.reset.description}
      error={mismatch ? t.access.passwordsDoNotMatch : reset.error?.message}
      submitLabel={reset.isPending ? t.access.reset.submitting : t.access.reset.submit}
      disabled={reset.isPending || mismatch}
      onSubmit={() => {
        if (mismatch) return;
        reset.mutate(
          { token: props.token, newPassword },
          // Signing them in here would contradict the sign-out this very request performs, and
          // would decide "stay signed in" for somebody who was never asked.
          { onSuccess: () => navigate({ to: "/login" }) },
        );
      }}
      footer={
        <Link
          to="/forgot-password"
          className="text-center text-[12.5px] text-[#8CA1B8] underline-offset-2 hover:text-[#DCE8F3] hover:underline"
        >
          {t.access.reset.askForNewLink}
        </Link>
      }
    >
      <Field
        label={t.access.reset.newPasswordLabel}
        type="password"
        value={newPassword}
        onChange={setNewPassword}
        minLength={8}
        autoFocus
      />
      <Field
        label={t.access.reset.repeatPasswordLabel}
        type="password"
        value={confirmation}
        onChange={setConfirmation}
      />
    </AuthCard>
  );
}
