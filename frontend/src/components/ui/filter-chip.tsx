import { XIcon } from "lucide-react";
import * as React from "react";

import { cn } from "@/lib/utils";

function FilterChip({
  className,
  onRemove,
  removeLabel,
  children,
  ...props
}: React.ComponentProps<"span"> & { onRemove?: () => void; removeLabel?: string }) {
  return (
    <span
      data-slot="filter-chip"
      className={cn(
        "bg-secondary text-secondary-foreground inline-flex h-7 min-w-0 max-w-full items-center gap-1 rounded-md border pr-1 pl-2 font-mono text-[11.5px] whitespace-nowrap",
        onRemove === undefined && "pr-2",
        className,
      )}
      {...props}
    >
      <span className="truncate">{children}</span>
      {onRemove !== undefined && (
        <button
          type="button"
          aria-label={removeLabel ?? "Remove filter"}
          className="text-muted-foreground hover:bg-accent hover:text-foreground rounded-sm p-0.5"
          onClick={onRemove}
        >
          <XIcon className="size-3" />
        </button>
      )}
    </span>
  );
}

export { FilterChip };
