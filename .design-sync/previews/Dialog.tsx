import {
  Button,
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  Input,
  Label,
} from "bugler-frontend";

export const Destructive = () => (
  <div className="min-h-[440px]">
    <Dialog defaultOpen>
      <DialogContent>
        <div className="grid gap-4">
          <DialogHeader>
            <DialogTitle>Delete service &quot;demo/prod · backend&quot;?</DialogTitle>
            <DialogDescription>
              This erases its API keys and every log and span it ever sent. Traces it shared with
              other services keep their remaining spans. This cannot be undone.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor="confirm-delete">
              Type <code className="font-mono text-foreground">demo/prod/backend</code> to confirm
            </Label>
            <Input id="confirm-delete" autoComplete="off" defaultValue="demo/prod/backend" />
          </div>

          <DialogFooter>
            <Button variant="ghost">Cancel</Button>
            <Button variant="destructive">Delete</Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  </div>
);

export const Default = () => (
  <div className="min-h-[400px]">
    <Dialog defaultOpen>
      <DialogContent>
        <div className="grid gap-4">
          <DialogHeader>
            <DialogTitle>Register application</DialogTitle>
            <DialogDescription>
              An application groups the services that report telemetry on its behalf.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor="application-name">Application name</Label>
            <Input id="application-name" placeholder="e.g. billing-api" defaultValue="checkout" />
          </div>

          <DialogFooter>
            <Button variant="ghost">Cancel</Button>
            <Button>Create application</Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  </div>
);

export const Acknowledge = () => (
  <div className="min-h-[320px]">
    <Dialog defaultOpen>
      <DialogContent showCloseButton={false} className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Ingestion paused</DialogTitle>
          <DialogDescription>
            The buffer is full, so exports are being rejected with 503 until it drains. No telemetry
            already accepted has been lost.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="secondary">Got it</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
);
