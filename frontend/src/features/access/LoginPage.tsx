import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import markDark from "@/bugler-mark-dark.svg";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

import { useAuthStatus, useLogin, useSetup } from "./useAuth";

export function LoginPage() {
  const status = useAuthStatus();

  if (status.isPending) {
    return <CenteredNote>Loading…</CenteredNote>;
  }

  return status.data?.needsSetup ? <SetupForm /> : <LoginForm />;
}

function LoginForm() {
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
        login.mutate({ email, password, staySignedIn }, { onSuccess: () => navigate({ to: "/" }) })
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
          { onSuccess: () => navigate({ to: "/" }) },
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
      <Field label="Password (min 8 characters)" type="password" value={password} onChange={setPassword} />
    </AuthCard>
  );
}

function AuthCard(props: {
  title: string;
  description: string;
  error: string | undefined;
  submitLabel: string;
  disabled: boolean;
  onSubmit: () => void;
  children: React.ReactNode;
}) {
  return (
    <div
      className="grid min-h-screen place-items-center p-8"
      style={{
        background:
          "radial-gradient(120% 80% at 50% -10%, rgba(233,164,60,0.10), transparent 60%)",
      }}
    >
      <div className="flex w-full max-w-[392px] flex-col gap-[26px]">
        <div className="flex items-center justify-center gap-2.5">
          <img src={markDark} alt="" className="size-[34px]" />
          <span className="text-[27px] font-semibold tracking-[-1.1px]">bugler</span>
        </div>

        <Card className="gap-5 rounded-xl border-[#1E344C] py-[26px] shadow-[0_18px_44px_-24px_#000]">
          <CardHeader>
            <CardTitle className="text-lg tracking-[-0.3px]">{props.title}</CardTitle>
            <CardDescription className="text-[12.5px] leading-normal">
              {props.description}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              className="grid gap-3.5"
              onSubmit={event => {
                event.preventDefault();
                props.onSubmit();
              }}
            >
              {props.children}
              {props.error !== undefined && (
                <p className="text-[12.5px] text-[#F0685A]">{props.error}</p>
              )}
              <Button type="submit" disabled={props.disabled} className="w-full">
                {props.submitLabel}
              </Button>
            </form>
          </CardContent>
        </Card>

        <p className="text-center font-mono text-[11px] text-[#5F7590]">bugler 0.1.0 · self-hosted</p>
      </div>
    </div>
  );
}

function Field(props: {
  label: string;
  type: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  autoFocus?: boolean;
}) {
  const id = props.label.toLowerCase().replace(/[^a-z]+/g, "-");
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{props.label}</Label>
      <Input
        id={id}
        type={props.type}
        value={props.value}
        onChange={event => props.onChange(event.target.value)}
        placeholder={props.placeholder}
        autoFocus={props.autoFocus}
        required
      />
    </div>
  );
}

function CenteredNote(props: { children: React.ReactNode }) {
  return <div className="grid min-h-screen place-items-center text-muted-foreground">{props.children}</div>;
}
