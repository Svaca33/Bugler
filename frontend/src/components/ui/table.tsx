import * as React from "react";

import { cn } from "@/lib/utils";

/**
 * The kit's table skeleton, styled to the navy console look the app's tables already wear:
 * an 8-row-high mono heading strip, hairline row borders, mono values. Purely presentational —
 * sorting and row models live in DataTable, which composes these parts.
 */

function Table({ className, ...props }: React.ComponentProps<"table">) {
  return (
    <div data-slot="table-container" className="relative w-full overflow-x-auto">
      <table
        data-slot="table"
        className={cn("w-full border-collapse", className)}
        {...props}
      />
    </div>
  );
}

function TableHeader({ className, ...props }: React.ComponentProps<"thead">) {
  return (
    <thead
      data-slot="table-header"
      className={cn(
        "bg-[#0B1826] font-mono text-[10px] tracking-[0.12em] text-[#5F7590] [&_tr]:h-8 [&_tr]:border-b [&_tr]:border-[#17293D]",
        className,
      )}
      {...props}
    />
  );
}

function TableBody({ className, ...props }: React.ComponentProps<"tbody">) {
  return (
    <tbody
      data-slot="table-body"
      className={cn("[&_tr]:h-10 [&_tr]:border-b [&_tr]:border-[#101F31]", className)}
      {...props}
    />
  );
}

function TableRow({ className, ...props }: React.ComponentProps<"tr">) {
  return <tr data-slot="table-row" className={cn(className)} {...props} />;
}

function TableHead({ className, ...props }: React.ComponentProps<"th">) {
  return (
    <th
      data-slot="table-head"
      className={cn("px-1.5 text-left font-normal whitespace-nowrap", className)}
      {...props}
    />
  );
}

function TableCell({ className, ...props }: React.ComponentProps<"td">) {
  return (
    <td
      data-slot="table-cell"
      className={cn("px-1.5 align-middle font-mono text-[11.5px] whitespace-nowrap", className)}
      {...props}
    />
  );
}

export { Table, TableBody, TableCell, TableHead, TableHeader, TableRow };
