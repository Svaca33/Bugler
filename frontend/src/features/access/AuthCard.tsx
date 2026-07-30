import markDark from "@/bugler-mark-dark.svg";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/**
 * The card every page reached without a Session wears: signing in, claiming a fresh server,
 * asking for a reset link and setting the new password all look like one place.
 */
export function AuthCard(props: {
  title: string;
  description: string;
  error?: string | undefined;
  submitLabel?: string;
  disabled?: boolean;
  onSubmit?: () => void;
  children?: React.ReactNode;
  footer?: React.ReactNode;
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
          <CardContent className="grid gap-3.5">
            {props.onSubmit !== undefined && (
              <form
                className="grid gap-3.5"
                onSubmit={event => {
                  event.preventDefault();
                  props.onSubmit?.();
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
            )}
            {props.onSubmit === undefined && props.children}
            {props.footer}
          </CardContent>
        </Card>

        <p className="text-center font-mono text-[11px] text-[#5F7590]">bugler · self-hosted</p>
      </div>
    </div>
  );
}

export function Field(props: {
  label: string;
  type: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  autoFocus?: boolean;
  minLength?: number;
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
        minLength={props.minLength}
        required
      />
    </div>
  );
}

export function CenteredNote(props: { children: React.ReactNode }) {
  return (
    <div className="grid min-h-screen place-items-center text-muted-foreground">{props.children}</div>
  );
}
