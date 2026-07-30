import { Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { AuthCard, Field } from "./AuthCard";
import { useResetPassword } from "./useAuth";

/**
 * Where the link from the mail lands. The secret rides in the query string and is only ever sent
 * back in a request body — a click from a mail is a GET, and nobody has typed a password yet.
 */
export function ResetPasswordPage(props: { token: string }) {
  const reset = useResetPassword();
  const navigate = useNavigate();
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");

  const mismatch = confirmation.length > 0 && newPassword !== confirmation;

  if (props.token.length === 0) {
    return (
      <AuthCard
        title="Something is missing from that link"
        description="This address carries no reset link. Open the one from the mail, or ask for a new one."
        footer={
          <Link
            to="/forgot-password"
            className="text-center text-[12.5px] text-[#8CA1B8] underline-offset-2 hover:text-[#DCE8F3] hover:underline"
          >
            Ask for a link
          </Link>
        }
      />
    );
  }

  return (
    <AuthCard
      title="Set a new password"
      description="Once it is set, every session of this account is signed out — sign in again with the new password."
      error={mismatch ? "The two do not match." : reset.error?.message}
      submitLabel={reset.isPending ? "Setting…" : "Set password"}
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
          Ask for a new link
        </Link>
      }
    >
      <Field
        label="New password (min 8 characters)"
        type="password"
        value={newPassword}
        onChange={setNewPassword}
        minLength={8}
        autoFocus
      />
      <Field
        label="Repeat new password"
        type="password"
        value={confirmation}
        onChange={setConfirmation}
      />
    </AuthCard>
  );
}
