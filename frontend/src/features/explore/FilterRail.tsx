import type { ReactNode } from "react";

/**
 * The left rail on Logs and Traces: every filter the list understands, stacked and grouped by
 * kind, with one Clear all. Nothing in it scrolls sideways — controls are full width.
 */
export function FilterRail(props: { canClear: boolean; onClear: () => void; children: ReactNode }) {
  return (
    <aside className="flex w-[254px] shrink-0 flex-col border-r border-[#17293D] bg-[#0B1826]">
      <div className="flex h-[42px] shrink-0 items-center gap-2 border-b border-[#17293D] px-4">
        <span className="text-[13px] font-semibold tracking-[-0.1px]">Filters</span>
        <button
          type="button"
          className="ml-auto font-mono text-[11px] text-[#7D93AA] hover:text-foreground disabled:opacity-40"
          onClick={props.onClear}
          disabled={!props.canClear}
        >
          Clear all
        </button>
      </div>
      <div className="flex min-h-0 flex-1 flex-col gap-[22px] overflow-auto p-4">{props.children}</div>
    </aside>
  );
}

/** One kind of filter: the label names it, the children are the controls that narrow by it. */
export function FilterGroup(props: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <span className="font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">{props.label}</span>
      {props.children}
    </div>
  );
}
