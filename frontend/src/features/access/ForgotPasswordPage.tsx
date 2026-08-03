import { Link } from "@tanstack/react-router";
import { useState } from "react";

import { useT } from "@/i18n";

import { AuthCard, Field } from "./AuthCard";
import { useForgotPassword } from "./useAuth";

/** Asking for a Reset Ticket. Its own page, so somebody still waiting has an address to come back to. */
export function ForgotPasswordPage() {
  const t = useT();
  const forgot = useForgotPassword();
  const [email, setEmail] = useState("");
  const [asked, setAsked] = useState(false);

  const backToSignIn = (
    <Link
      to="/login"
      className="text-center text-[12.5px] text-[#8CA1B8] underline-offset-2 hover:text-[#DCE8F3] hover:underline"
    >
      {t.access.forgot.backToSignIn}
    </Link>
  );

  if (asked) {
    return (
      <AuthCard
        title={t.access.forgot.sent.title}
        description={t.access.forgot.sent.description}
        footer={backToSignIn}
      >
        <p className="text-[12.5px] leading-[1.55] text-[#8CA1B8]">
          {t.access.forgot.nothingArrived(
            <button
              type="button"
              className="text-[#DCE8F3] underline underline-offset-2"
              onClick={() => {
                setAsked(false);
                forgot.reset();
              }}
            >
              {t.access.forgot.askAgain}
            </button>,
          )}
        </p>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title={t.access.forgot.title}
      description={t.access.forgot.description}
      error={forgot.error?.message}
      submitLabel={forgot.isPending ? t.access.forgot.submitting : t.access.forgot.submit}
      disabled={forgot.isPending}
      onSubmit={() => forgot.mutate({ email }, { onSuccess: () => setAsked(true) })}
      footer={backToSignIn}
    >
      <Field
        label={t.access.emailLabel}
        type="email"
        value={email}
        onChange={setEmail}
        placeholder={t.access.emailPlaceholder}
        autoFocus
      />
    </AuthCard>
  );
}
