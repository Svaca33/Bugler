import { Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { useT } from "@/i18n";

import { AuthCard, CenteredNote, Field } from "./AuthCard";
import { useAuthStatus, useLogin, useSetup } from "./useAuth";

/**
 * The two doors that put somebody inside: signing in, and — on a server nobody has claimed yet —
 * creating the first account. Both end at the same place, the address the visitor was heading for
 * when they were turned away, which the route has already vouched for.
 */
export function LoginPage(props: { destination: string }) {
  const t = useT();
  const status = useAuthStatus();

  if (status.isPending) {
    return <CenteredNote>{t.common.loading}</CenteredNote>;
  }

  return status.data?.needsSetup ? (
    <SetupForm destination={props.destination} />
  ) : (
    <LoginForm
      destination={props.destination}
      resetAvailable={status.data?.passwordResetAvailable === true}
    />
  );
}

function LoginForm(props: { destination: string; resetAvailable: boolean }) {
  const t = useT();
  const login = useLogin();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [staySignedIn, setStaySignedIn] = useState(false);

  return (
    <AuthCard
      title={t.access.login.title}
      description={t.access.login.description}
      error={login.error?.message}
      submitLabel={login.isPending ? t.access.login.submitting : t.access.login.submit}
      disabled={login.isPending}
      onSubmit={() =>
        login.mutate(
          { email, password, staySignedIn },
          // Replacing rather than pushing: the sign-in is a step nobody wants to walk back into.
          { onSuccess: () => navigate({ href: props.destination, replace: true }) },
        )
      }
      footer={
        // Hidden on a server without SMTP: a link that always promises a mail nobody receives
        // is worse than no link at all.
        props.resetAvailable ? (
          <Link
            to="/forgot-password"
            className="text-center text-[12.5px] text-[#8CA1B8] underline-offset-2 hover:text-[#DCE8F3] hover:underline"
          >
            {t.access.login.forgotPassword}
          </Link>
        ) : undefined
      }
    >
      <Field
        label={t.access.emailLabel}
        type="email"
        value={email}
        onChange={setEmail}
        placeholder={t.access.emailPlaceholder}
        autoFocus
      />
      <Field
        label={t.access.login.passwordLabel}
        type="password"
        value={password}
        onChange={setPassword}
      />
      <div className="flex items-center gap-2">
        <Checkbox
          id="stay-signed-in"
          checked={staySignedIn}
          onCheckedChange={checked => setStaySignedIn(checked === true)}
        />
        <Label htmlFor="stay-signed-in" className="font-normal">
          {t.access.login.staySignedIn}
        </Label>
      </div>
    </AuthCard>
  );
}

function SetupForm(props: { destination: string }) {
  const t = useT();
  const setup = useSetup();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");

  return (
    <AuthCard
      title={t.access.setup.title}
      description={t.access.setup.description}
      error={setup.error?.message}
      submitLabel={setup.isPending ? t.access.setup.submitting : t.access.setup.submit}
      disabled={setup.isPending}
      onSubmit={() =>
        setup.mutate(
          { email, password, displayName: displayName || undefined },
          { onSuccess: () => navigate({ href: props.destination, replace: true }) },
        )
      }
    >
      <Field
        label={t.access.setup.nameLabel}
        type="text"
        value={displayName}
        onChange={setDisplayName}
        autoFocus
      />
      <Field
        label={t.access.emailLabel}
        type="email"
        value={email}
        onChange={setEmail}
        placeholder={t.access.emailPlaceholder}
      />
      <Field
        label={t.access.setup.passwordLabel}
        type="password"
        value={password}
        onChange={setPassword}
        minLength={8}
      />
    </AuthCard>
  );
}
