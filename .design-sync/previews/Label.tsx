import { Input, Label, Textarea } from "bugler-frontend";

export const Default = () => (
  <div className="grid w-full max-w-sm gap-2">
    <Label htmlFor="service">Service name</Label>
    <Input id="service" placeholder="billing-service" />
  </div>
);

export const WithHint = () => (
  <div className="grid w-full max-w-sm gap-2">
    <Label htmlFor="retention">
      Retention window
      <span className="text-muted-foreground font-normal">(days)</span>
    </Label>
    <Input id="retention" type="number" defaultValue={30} />
  </div>
);

export const OnDisabledField = () => (
  <div className="grid w-full max-w-sm gap-2">
    <Label htmlFor="plan">Subscription plan</Label>
    <Textarea id="plan" disabled defaultValue="Team — managed by your organization admin." />
  </div>
);
