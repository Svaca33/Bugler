import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "bugler-frontend";

/*
 * Every story wraps the group in a box with a definite height. The group writes `height: 100%` on
 * itself inline, so a height utility placed on the group is ignored — the height has to come from
 * the parent, exactly as it does in the app (a flex column that already has one).
 */

/** The arrangement this primitive exists for: a list you read, a detail you open beside it. */
export const LogDetail = () => (
  <div className="h-56 w-full">
    <ResizablePanelGroup className="rounded-md border">
      <ResizablePanel minSize="220px" className="flex h-full flex-col gap-2 p-4">
        <span className="text-sm font-medium">Log records</span>
        <div className="flex flex-col gap-1">
          <span className="font-mono text-xs text-muted-foreground">09:41:02 ERROR checkout</span>
          <span className="font-mono text-xs text-muted-foreground">09:41:02 WARN inventory</span>
          <span className="font-mono text-xs text-muted-foreground">09:40:58 INFO checkout</span>
        </div>
      </ResizablePanel>
      <ResizableHandle withHandle />
      <ResizablePanel defaultSize={240} minSize="180px" className="flex h-full flex-col gap-2 p-4">
        <span className="text-sm font-medium">Log record</span>
        <span className="font-mono text-xs text-muted-foreground">
          Payment declined for order 4471
        </span>
      </ResizablePanel>
    </ResizablePanelGroup>
  </div>
);

/** Without `withHandle` the divider is a hairline that only answers to the cursor. */
export const PlainDivider = () => (
  <div className="h-56 w-full">
    <ResizablePanelGroup className="rounded-md border">
      <ResizablePanel className="flex h-full items-center justify-center p-4">
        <span className="text-sm text-muted-foreground">Waterfall</span>
      </ResizablePanel>
      <ResizableHandle />
      <ResizablePanel defaultSize={240} className="flex h-full items-center justify-center p-4">
        <span className="text-sm text-muted-foreground">Span</span>
      </ResizablePanel>
    </ResizablePanelGroup>
  </div>
);

/** Stacked panels: the same primitive with the group's axis turned. */
export const Vertical = () => (
  <div className="h-56 w-full">
    <ResizablePanelGroup orientation="vertical" className="rounded-md border">
      <ResizablePanel className="flex items-center justify-center p-4">
        <span className="text-sm text-muted-foreground">Volume</span>
      </ResizablePanel>
      <ResizableHandle withHandle />
      <ResizablePanel className="flex items-center justify-center p-4">
        <span className="text-sm text-muted-foreground">Records</span>
      </ResizablePanel>
    </ResizablePanelGroup>
  </div>
);
