import { ZoomInIcon, ZoomOutIcon } from "lucide-react";

import {
  displayPath,
  flattenLeaves,
  isActive,
  leafText,
  type AttributeFilter,
  type FilterScope,
} from "./attributeFilters";

/** Detail-panel rows for one jsonb attributes object; scalar leaves carry a +/− magnifier. */
export function AttributeLeafList(props: {
  attributes: unknown;
  scope: FilterScope;
  filters: AttributeFilter[];
  onToggle: (filter: AttributeFilter, active: boolean) => void;
}) {
  const leaves = flattenLeaves(props.attributes);
  if (leaves.length === 0) {
    return <p className="px-2.5 font-mono text-[11.5px] text-[#5F7590]">—</p>;
  }

  return (
    <div className="grid gap-px rounded-[7px] bg-[#16283C] p-1.5">
      {leaves.map(leaf => {
        const filter = leaf.isScalar
          ? { scope: props.scope, path: leaf.path, value: leafText(leaf.value) }
          : null;
        const active = filter !== null && isActive(props.filters, filter);
        const label = displayPath(leaf.path);
        return (
          <div
            key={label}
            className="flex items-start gap-1.5 rounded-[5px] px-1 py-[3px] hover:bg-[#1C3049]"
          >
            {filter !== null ? (
              <button
                type="button"
                aria-label={active ? `Stop filtering by ${label}` : `Filter by ${label}`}
                title={active ? "Remove this filter" : "Filter by this value"}
                className={`mt-[2px] shrink-0 ${active ? "text-primary" : "text-[#5F7590] hover:text-foreground"}`}
                onClick={() => props.onToggle(filter, active)}
              >
                {active ? <ZoomOutIcon className="size-3.5" /> : <ZoomInIcon className="size-3.5" />}
              </button>
            ) : (
              <span className="mt-[2px] size-3.5 shrink-0" />
            )}
            <span className="shrink-0 font-mono text-[11px] leading-5 text-[#7D93AA]">{label}</span>
            <span className="min-w-0 break-all font-mono text-[11.5px] leading-5 text-[#DCE8F3]">
              {leaf.isScalar ? leafText(leaf.value) : JSON.stringify(leaf.value)}
            </span>
          </div>
        );
      })}
    </div>
  );
}
