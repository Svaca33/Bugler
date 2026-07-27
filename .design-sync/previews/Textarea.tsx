import { Label, Textarea } from "bugler-frontend";

export const Field = () => (
  <div className="grid w-full max-w-md gap-2">
    <Label htmlFor="notes">Notes</Label>
    <Textarea id="notes" placeholder="What happened before the error?" />
  </div>
);

export const WithContent = () => (
  <div className="grid w-full max-w-md gap-2">
    <Label htmlFor="description">Incident description</Label>
    <Textarea
      id="description"
      defaultValue={
        "Checkout requests started returning 502 at 14:32 UTC.\nRollback to v2.14.1 restored service at 14:47.\nRoot cause: connection pool exhaustion after the retry storm."
      }
    />
  </div>
);

export const States = () => (
  <div className="grid w-full max-w-md gap-4">
    <div className="grid gap-2">
      <Label htmlFor="reason">Reason</Label>
      <Textarea id="reason" aria-invalid placeholder="Required" />
      <p className="text-destructive text-sm">A reason is required to purge logs.</p>
    </div>
    <div className="grid gap-2">
      <Label htmlFor="audit">Audit note</Label>
      <Textarea id="audit" disabled defaultValue="Read-only: audit notes are locked after submission." />
    </div>
  </div>
);
