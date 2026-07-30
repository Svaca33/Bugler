import { Link } from "@tanstack/react-router";
import { useState } from "react";

import { AuthCard, Field } from "./AuthCard";
import { useForgotPassword } from "./useAuth";

/** Asking for a Reset Ticket. Its own page, so somebody still waiting has an address to come back to. */
export function ForgotPasswordPage() {
  const forgot = useForgotPassword();
  const [email, setEmail] = useState("");
  const [asked, setAsked] = useState(false);

  const backToSignIn = (
    <Link
      to="/login"
      className="text-center text-[12.5px] text-[#8CA1B8] underline-offset-2 hover:text-[#DCE8F3] hover:underline"
    >
      Back to sign in
    </Link>
  );

  if (asked) {
    return (
      <AuthCard
        title="Check your mail"
        description="If an account uses that address, a link to set a new password is on its way to it. The link works once and stops working in an hour."
        footer={backToSignIn}
      >
        <p className="text-[12.5px] leading-[1.55] text-[#8CA1B8]">
          Nothing arrived? Wait a couple of minutes, then{" "}
          <button
            type="button"
            className="text-[#DCE8F3] underline underline-offset-2"
            onClick={() => {
              setAsked(false);
              forgot.reset();
            }}
          >
            ask again
          </button>
          .
        </p>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title="Forgot your password?"
      description="Give the address your account uses and Bugler will mail you a link to set a new password."
      error={forgot.error?.message}
      submitLabel={forgot.isPending ? "Sending…" : "Send the link"}
      disabled={forgot.isPending}
      onSubmit={() => forgot.mutate({ email }, { onSuccess: () => setAsked(true) })}
      footer={backToSignIn}
    >
      <Field
        label="E-mail"
        type="email"
        value={email}
        onChange={setEmail}
        placeholder="you@company.com"
        autoFocus
      />
    </AuthCard>
  );
}
