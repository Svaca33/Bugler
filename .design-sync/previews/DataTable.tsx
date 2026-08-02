import { DataTable } from "bugler-frontend";

/*
 * DataTable owns the sorting; the preview supplies columns and rows the way a page would.
 * Sort keys are the numeric fields, the cells render preformatted labels. Dark-committed like
 * the Table parts it composes, so stories render inside `.dark`. No `persistKey` here — a
 * preview must not write anyone's localStorage.
 */

const services = [
  { name: "Shop · prod · checkout", logs: 1_200, logsLabel: "≈ 1.2 GB", traces: 340, tracesLabel: "≈ 340 MB" },
  { name: "Shop · prod · payments", logs: 810, logsLabel: "≈ 810 MB", traces: 1_100, tracesLabel: "≈ 1.1 GB" },
  { name: "Shop · staging · checkout", logs: 96, logsLabel: "≈ 96 MB", traces: 0, tracesLabel: "—" },
];

const columns = [
  {
    id: "service",
    accessorFn: row => row.name,
    header: "SERVICE",
    sortDescFirst: false,
    meta: { headerClassName: "w-full pl-4", cellClassName: "w-full pl-4 text-xs" },
    cell: context => context.row.original.name,
  },
  {
    id: "logs",
    accessorFn: row => row.logs,
    header: "LOGS",
    sortDescFirst: true,
    meta: { headerClassName: "text-right", cellClassName: "text-right" },
    cell: context => context.row.original.logsLabel,
  },
  {
    id: "traces",
    accessorFn: row => row.traces,
    header: "TRACES",
    sortDescFirst: true,
    meta: { headerClassName: "pr-4 text-right", cellClassName: "pr-4 text-right" },
    cell: context => context.row.original.tracesLabel,
  },
];

/** Opens on the default order — heaviest logs first — until a heading is clicked. */
export const Sorted = () => (
  <div className="dark w-full bg-background p-4 text-foreground">
    <div className="overflow-hidden rounded-[11px] border border-[#1E344C] bg-card">
      <DataTable columns={columns} data={services} defaultSort={[{ id: "logs", desc: true }]} />
    </div>
  </div>
);
