import { DetailPanel, ResizablePanel, ResizablePanelGroup } from "bugler-frontend";

/*
 * DetailPanel renders its own divider next to its own panel, so it must sit as a direct child of
 * a ResizablePanelGroup, after the list panel — a Fragment adds no element, a wrapper div breaks
 * the group. The group needs a parent with a definite height, same as every panel group.
 *
 * Stories render inside `.dark`: the panel's chrome colours are tuned for the navy console theme
 * the app pins itself to, and on the light theme its foreground text would sit dark-on-navy.
 */

/** The arrangement the chrome exists for: a list on the left, the record you opened beside it. */
export const ListAndDetail = () => (
  <div className="dark h-80 w-full bg-background text-foreground">
    <ResizablePanelGroup className="rounded-md border">
      <ResizablePanel minSize="220px" className="flex h-full flex-col gap-1 p-4">
        <span className="text-sm font-medium">Log records</span>
        <div className="flex flex-col gap-1">
          <span className="font-mono text-xs text-muted-foreground">09:41:02 ERROR checkout</span>
          <span className="font-mono text-xs text-severity-error">09:41:02 ERROR payments</span>
          <span className="font-mono text-xs text-muted-foreground">09:40:58 INFO checkout</span>
        </div>
      </ResizablePanel>
      <DetailPanel
        title={<span className="text-sm font-semibold">Log record</span>}
        onClose={() => {}}
      >
        <div className="flex flex-col gap-2">
          <span className="font-mono text-xs text-muted-foreground">
            31/07/2026 09:41:02.482
          </span>
          <p className="font-mono text-sm">Payment declined for order 4471: insufficient funds</p>
        </div>
      </DetailPanel>
    </ResizablePanelGroup>
  </div>
);

/** The title slot takes any node — a badge and a duration read as one line of chrome. */
export const BadgedTitle = () => (
  <div className="dark h-80 w-full bg-background text-foreground">
    <ResizablePanelGroup className="rounded-md border">
      <ResizablePanel minSize="220px" className="flex h-full flex-col gap-1 p-4">
        <span className="text-sm font-medium">Episodes</span>
        <span className="font-mono text-xs text-muted-foreground">
          Timeout waiting for payment authorization
        </span>
      </ResizablePanel>
      <DetailPanel
        title={
          <span className="flex items-center gap-2">
            <span className="font-mono text-xs text-severity-error uppercase">open</span>
            <span className="font-mono text-xs text-muted-foreground">· 14 min 21 s</span>
          </span>
        }
        onClose={() => {}}
      >
        <p className="font-mono text-sm">Timeout waiting for payment authorization</p>
      </DetailPanel>
    </ResizablePanelGroup>
  </div>
);
