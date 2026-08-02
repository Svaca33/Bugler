import {
  flexRender,
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type OnChangeFn,
  type RowData,
  type SortingState,
} from "@tanstack/react-table";
import { ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { readTableSort, writeTableSort } from "@/lib/tableSort";

declare module "@tanstack/react-table" {
  interface ColumnMeta<TData extends RowData, TValue> {
    /** Extra classes for this column's `th` — alignment and edge padding. */
    headerClassName?: string;
    /** Extra classes for every `td` in this column. */
    cellClassName?: string;
    /** Tooltip on the column heading. */
    headerTitle?: string;
  }
}

/**
 * A sortable table over the kit's Table parts: pages supply column definitions and rows, this
 * owns the sorting. One column at a time, no unsorted state — `defaultSort` is always in force
 * until a heading is clicked. With `persistKey` the reader's choice is remembered in the browser
 * and restored next visit; a remembered column the table no longer sorts by falls back to the
 * default. Ties keep the incoming row order, so pass rows in a deterministic order.
 */
export function DataTable<TData>(props: {
  columns: ColumnDef<TData>[];
  data: TData[];
  defaultSort: SortingState;
  persistKey?: string;
  rowTestId?: string;
}) {
  const { columns, defaultSort, persistKey } = props;
  const [sorting, setSorting] = useState<SortingState>(() => {
    if (persistKey === undefined) return defaultSort;
    const sortable = columns
      .map(column => column.id)
      .filter((id): id is string => id !== undefined);
    const saved = readTableSort(persistKey, sortable);
    return saved === undefined ? defaultSort : [{ id: saved.column, desc: saved.desc }];
  });

  const onSortingChange: OnChangeFn<SortingState> = updater => {
    const next = typeof updater === "function" ? updater(sorting) : updater;
    setSorting(next);
    const first = next[0];
    if (persistKey !== undefined && first !== undefined) {
      writeTableSort(persistKey, { column: first.id, desc: first.desc });
    }
  };

  const table = useReactTable({
    data: props.data,
    columns,
    state: { sorting },
    onSortingChange,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    enableSortingRemoval: false,
    enableMultiSort: false,
  });

  return (
    <Table>
      <TableHeader>
        {table.getHeaderGroups().map(headerGroup => (
          <TableRow key={headerGroup.id}>
            {headerGroup.headers.map(header => {
              const meta = header.column.columnDef.meta;
              const sorted = header.column.getIsSorted();
              return (
                <TableHead
                  key={header.id}
                  title={meta?.headerTitle}
                  aria-sort={
                    sorted === "asc" ? "ascending" : sorted === "desc" ? "descending" : undefined
                  }
                  className={meta?.headerClassName}
                >
                  {header.column.getCanSort() ? (
                    <button
                      type="button"
                      className="inline-flex cursor-pointer items-center gap-1 outline-none select-none hover:text-[#8CA1B8] focus-visible:text-[#8CA1B8]"
                      onClick={header.column.getToggleSortingHandler()}
                    >
                      {flexRender(header.column.columnDef.header, header.getContext())}
                      {sorted === "asc" ? (
                        <ChevronUp className="size-2.5" aria-hidden />
                      ) : sorted === "desc" ? (
                        <ChevronDown className="size-2.5" aria-hidden />
                      ) : null}
                    </button>
                  ) : (
                    flexRender(header.column.columnDef.header, header.getContext())
                  )}
                </TableHead>
              );
            })}
          </TableRow>
        ))}
      </TableHeader>
      <TableBody>
        {table.getRowModel().rows.map(row => (
          <TableRow key={row.id} data-testid={props.rowTestId}>
            {row.getVisibleCells().map(cell => (
              <TableCell key={cell.id} className={cell.column.columnDef.meta?.cellClassName}>
                {flexRender(cell.column.columnDef.cell, cell.getContext())}
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
