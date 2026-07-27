import { Button } from "bugler-frontend";
import { PlusIcon, RefreshCwIcon, Trash2Icon, ExternalLinkIcon } from "lucide-react";

export const Variants = () => (
  <div className="flex flex-wrap items-center gap-2">
    <Button>Save changes</Button>
    <Button variant="secondary">Export</Button>
    <Button variant="outline">Cancel</Button>
    <Button variant="ghost">Dismiss</Button>
    <Button variant="destructive">Delete user</Button>
    <Button variant="link">View trace</Button>
  </div>
);

export const Sizes = () => (
  <div className="flex flex-wrap items-center gap-2">
    <Button size="sm">Small</Button>
    <Button size="default">Default</Button>
    <Button size="lg">Large</Button>
    <Button size="icon-sm" aria-label="Add application">
      <PlusIcon />
    </Button>
    <Button size="icon" aria-label="Refresh" variant="outline">
      <RefreshCwIcon />
    </Button>
    <Button size="icon-lg" aria-label="Delete" variant="ghost">
      <Trash2Icon />
    </Button>
  </div>
);

export const WithIcons = () => (
  <div className="flex flex-wrap items-center gap-2">
    <Button>
      <PlusIcon />
      Add application
    </Button>
    <Button variant="secondary">
      <RefreshCwIcon />
      Refresh
    </Button>
    <Button variant="outline">
      Open in Explore
      <ExternalLinkIcon />
    </Button>
  </div>
);

export const States = () => (
  <div className="flex flex-wrap items-center gap-2">
    <Button disabled>Saving…</Button>
    <Button variant="outline" disabled>
      Cancel
    </Button>
    <Button variant="destructive" size="sm" disabled>
      Delete
    </Button>
  </div>
);
