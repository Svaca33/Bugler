import { GripVerticalIcon } from "lucide-react";
import * as React from "react";
import * as ResizablePrimitive from "react-resizable-panels";

import { cn } from "@/lib/utils";

function ResizablePanelGroup({
  className,
  ...props
}: React.ComponentProps<typeof ResizablePrimitive.Group>) {
  return (
    <ResizablePrimitive.Group
      data-slot="resizable-panel-group"
      className={cn("flex h-full w-full", className)}
      {...props}
    />
  );
}

function ResizablePanel({ ...props }: React.ComponentProps<typeof ResizablePrimitive.Panel>) {
  return <ResizablePrimitive.Panel data-slot="resizable-panel" {...props} />;
}

/**
 * The draggable divider. `aria-orientation` describes the separator's own axis, not the group's:
 * a "vertical" separator is the line drawn between side-by-side panels, i.e. a horizontal group.
 *
 * The library reports its interaction state on `data-separator`
 * (`inactive` | `hover` | `active` | `focus` | `disabled`) — that attribute, not `:hover`, is what
 * the styles below key off, so the divider lights up across the whole hit target rather than only
 * over its one visible pixel.
 */
function ResizableHandle({
  withHandle,
  className,
  ...props
}: React.ComponentProps<typeof ResizablePrimitive.Separator> & { withHandle?: boolean }) {
  return (
    <ResizablePrimitive.Separator
      data-slot="resizable-handle"
      className={cn(
        "group relative flex items-center justify-center bg-border outline-none transition-colors",
        "aria-[orientation=vertical]:w-px aria-[orientation=horizontal]:h-px aria-[orientation=horizontal]:w-full",
        "data-[separator=hover]:bg-primary data-[separator=active]:bg-primary data-[separator=focus]:bg-primary",
        className,
      )}
      {...props}
    >
      {withHandle && (
        <div
          data-slot="resizable-grip"
          className={cn(
            "z-10 flex h-4 w-3 items-center justify-center rounded-sm border bg-muted text-muted-foreground transition-colors",
            "group-data-[separator=hover]:border-primary group-data-[separator=hover]:text-primary",
            "group-data-[separator=active]:border-primary group-data-[separator=active]:text-primary",
            "group-data-[separator=focus]:border-primary group-data-[separator=focus]:text-primary",
          )}
        >
          <GripVerticalIcon className="size-2.5 group-aria-[orientation=horizontal]:rotate-90" />
        </div>
      )}
    </ResizablePrimitive.Separator>
  );
}

// `Resizable` is the design-system card name for this compound, and the converter looks up
// `window.Bugler.Resizable` verbatim — so the root must actually be exported under it. Same
// arrangement as `Chart`/`ChartContainer`; a shadcn regeneration will drop this line.
export { ResizableHandle, ResizablePanel, ResizablePanelGroup, ResizablePanelGroup as Resizable };
