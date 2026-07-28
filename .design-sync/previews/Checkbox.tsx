import { Checkbox, Label } from "bugler-frontend";

export const Default = () => (
  <div className="flex items-center gap-2">
    <Checkbox id="stay-signed-in" />
    <Label htmlFor="stay-signed-in" className="font-normal">
      Stay signed in
    </Label>
  </div>
);

export const Checked = () => (
  <div className="flex items-center gap-2">
    <Checkbox id="follow-tail" defaultChecked />
    <Label htmlFor="follow-tail" className="font-normal">
      Follow new records
    </Label>
  </div>
);

export const Group = () => (
  <div className="grid w-full max-w-sm gap-2">
    <Label>Severity</Label>
    {[
      { id: "sev-error", label: "Error", checked: true },
      { id: "sev-warn", label: "Warning", checked: true },
      { id: "sev-info", label: "Info", checked: false },
      { id: "sev-debug", label: "Debug", checked: false },
    ].map(severity => (
      <div key={severity.id} className="flex items-center gap-2">
        <Checkbox id={severity.id} defaultChecked={severity.checked} />
        <Label htmlFor={severity.id} className="font-normal">
          {severity.label}
        </Label>
      </div>
    ))}
  </div>
);

export const Disabled = () => (
  <div className="grid w-full max-w-sm gap-2">
    <div className="flex items-center gap-2">
      <Checkbox id="purge-now" disabled />
      <Label htmlFor="purge-now" className="font-normal">
        Purge immediately
      </Label>
    </div>
    <p className="text-muted-foreground text-sm">Requires an administrator account.</p>
  </div>
);
