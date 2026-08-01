import { Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";

import { AuthCard, CenteredNote, Field } from "./AuthCard";
import { useAuthStatus, useLogin, useSetup } from "./useAuth";

export function LoginPage() {
  const status = useAuthStatus();

  if (status.isPending) {
    return <CenteredNote>Loading…</CenteredNote>;
  }

  return status.data?.needsSetup ? (
    <SetupForm />
  ) : (
    <LoginForm resetAvailable={status.data?.passwordResetAvailable === true} />
  );
}

function LoginForm(props: { resetAvailable: boolean }) {
  const login = useLogin();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [staySignedIn, setStaySignedIn] = useState(false);

  return (
    <AuthCard
      title="Sign in to Bugler"
      description="Use your local Bugler account."
      error={login.error?.message}
      submitLabel={login.isPending ? "Signing in…" : "Sign in"}
      disabled={login.isPending}
      onSubmit={() =>
        login.mutate({ email, password, staySignedIn }, { onSuccess: () => navigate({ to: "/dashboard" }) })
      }
      footer={
        // Hidden on a server without SMTP: a link that always promises a mail nobody receives
        // is worse than no link at all.
        props.resetAvailable ? (
          <Link
            to="/forgot-password"
            className="text-center text-[12.5px] text-[#8CA1B8] underline-offset-2 hover:text-[#DCE8F3] hover:underline"
          >
            Forgot your password?
          </Link>
        ) : undefined
      }
    >
      <Field
        label="E-mail"
        type="email"
        value={email}
        onChange={setEmail}
        placeholder="you@company.com"
        autoFocus
      />
      <Field label="Password" type="password" value={password} onChange={setPassword} />
      <div className="flex items-center gap-2">
        <Checkbox
          id="stay-signed-in"
          checked={staySignedIn}
          onCheckedChange={checked => setStaySignedIn(checked === true)}
        />
        <Label htmlFor="stay-signed-in" className="font-normal">
          Stay signed in
        </Label>
      </div>
    </AuthCard>
  );
}

function SetupForm() {
  const setup = useSetup();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");

  return (
    <AuthCard
      title="Welcome to Bugler"
      description="Create the first account. It becomes the server administrator."
      error={setup.error?.message}
      submitLabel={setup.isPending ? "Creating…" : "Create admin account"}
      disabled={setup.isPending}
      onSubmit={() =>
        setup.mutate(
          { email, password, displayName: displayName || undefined },
          { onSuccess: () => navigate({ to: "/dashboard" }) },
        )
      }
    >
      <Field label="Name" type="text" value={displayName} onChange={setDisplayName} autoFocus />
      <Field
        label="E-mail"
        type="email"
        value={email}
        onChange={setEmail}
        placeholder="you@company.com"
      />
      <Field
        label="Password (min 8 characters)"
        type="password"
        value={password}
        onChange={setPassword}
        minLength={8}
      />
    </AuthCard>
  );
}
