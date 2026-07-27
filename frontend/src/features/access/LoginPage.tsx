import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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

  return (
    <AuthCard
      title="Sign in to Bugler"
      description="Use your local Bugler account."
      error={login.error?.message}
      submitLabel={login.isPending ? "Signing in…" : "Sign in"}
      disabled={login.isPending}
      onSubmit={() => login.mutate({ email, password }, { onSuccess: () => navigate({ to: "/" }) })}
    >
      <Field label="E-mail" type="email" value={email} onChange={setEmail} autoFocus />
      <Field label="Password" type="password" value={password} onChange={setPassword} />
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
      <Field label="E-mail" type="email" value={email} onChange={setEmail} />
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
    <div className="grid min-h-screen place-items-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>{props.title}</CardTitle>
          <CardDescription>{props.description}</CardDescription>
        </CardHeader>
        <CardContent>
          <form
            className="grid gap-4"
            onSubmit={event => {
              event.preventDefault();
              props.onSubmit();
            }}
          >
            {props.children}
            {props.error !== undefined && <p className="text-sm text-destructive">{props.error}</p>}
            <Button type="submit" disabled={props.disabled}>
              {props.submitLabel}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

function Field(props: {
  label: string;
  type: string;
  value: string;
  onChange: (value: string) => void;
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
        autoFocus={props.autoFocus}
        required
      />
    </div>
  );
}

function CenteredNote(props: { children: React.ReactNode }) {
  return <div className="grid min-h-screen place-items-center text-muted-foreground">{props.children}</div>;
}
