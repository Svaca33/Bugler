import { Input, Label } from "bugler-frontend";

export const Field = () => (
  <div className="grid w-full max-w-sm gap-2">
    <Label htmlFor="app-name">Application name</Label>
    <Input id="app-name" placeholder="checkout-service" />
  </div>
);

export const Types = () => (
  <div className="grid w-full max-w-sm gap-4">
    <div className="grid gap-2">
      <Label htmlFor="email">Email</Label>
      <Input id="email" type="email" defaultValue="ondrej@bugler.dev" />
    </div>
    <div className="grid gap-2">
      <Label htmlFor="password">Password</Label>
      <Input id="password" type="password" defaultValue="hunter2secret" />
    </div>
    <div className="grid gap-2">
      <Label htmlFor="sourcemap">Source map</Label>
      <Input id="sourcemap" type="file" />
    </div>
  </div>
);

export const States = () => (
  <div className="grid w-full max-w-sm gap-4">
    <div className="grid gap-2">
      <Label htmlFor="api-key">API key</Label>
      <Input id="api-key" aria-invalid defaultValue="blgr_live_…truncated" />
      <p className="text-destructive text-sm">This key was revoked on 12 July.</p>
    </div>
    <div className="grid gap-2">
      <Label htmlFor="org">Organization</Label>
      <Input id="org" disabled defaultValue="bugler-labs" />
    </div>
  </div>
);
