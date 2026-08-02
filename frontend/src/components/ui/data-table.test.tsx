import { afterEach, expect, test } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";
import type { ColumnDef } from "@tanstack/react-table";

import { DataTable } from "./data-table";

interface Fruit {
  name: string;
  weight: number;
}

const columns: ColumnDef<Fruit>[] = [
  { id: "name", accessorFn: row => row.name, header: "NAME", sortDescFirst: false },
  { id: "weight", accessorFn: row => row.weight, header: "WEIGHT", sortDescFirst: true },
];

const fruits: Fruit[] = [
  { name: "apple", weight: 120 },
  { name: "cherry", weight: 8 },
  { name: "banana", weight: 150 },
];

function renderTable(persistKey?: string) {
  return render(
    <DataTable
      columns={columns}
      data={fruits}
      defaultSort={[{ id: "weight", desc: true }]}
      persistKey={persistKey}
      rowTestId="fruit"
    />,
  );
}

function names(): string[] {
  return screen
    .getAllByTestId("fruit")
    .map(row => (row as HTMLTableRowElement).cells[0]?.textContent ?? "");
}

afterEach(() => localStorage.clear());

test("rows open in the default order, heaviest first", () => {
  renderTable();

  expect(names()).toEqual(["banana", "apple", "cherry"]);
  expect(screen.getByRole("button", { name: "WEIGHT" }).closest("th")!.getAttribute("aria-sort")).toBe(
    "descending",
  );
});

test("a heading click sorts by that column, its own direction first", () => {
  renderTable();

  fireEvent.click(screen.getByRole("button", { name: "NAME" }));
  expect(names()).toEqual(["apple", "banana", "cherry"]);
  expect(screen.getByRole("button", { name: "NAME" }).closest("th")!.getAttribute("aria-sort")).toBe(
    "ascending",
  );

  fireEvent.click(screen.getByRole("button", { name: "NAME" }));
  expect(names()).toEqual(["cherry", "banana", "apple"]);
});

test("there is no unsorted state — a third click circles back instead of clearing", () => {
  renderTable();
  const name = () => screen.getByRole("button", { name: "NAME" });

  fireEvent.click(name());
  fireEvent.click(name());
  fireEvent.click(name());

  expect(names()).toEqual(["apple", "banana", "cherry"]);
  expect(name().closest("th")!.getAttribute("aria-sort")).toBe("ascending");
});

test("with a persist key the chosen sort is remembered and restored", () => {
  const first = renderTable("bugler.test.fruit-sort");
  fireEvent.click(screen.getByRole("button", { name: "NAME" }));
  expect(localStorage.getItem("bugler.test.fruit-sort")).toBe('{"column":"name","desc":false}');
  first.unmount();

  renderTable("bugler.test.fruit-sort");
  expect(names()).toEqual(["apple", "banana", "cherry"]);
});

test("a remembered column the table no longer has falls back to the default sort", () => {
  localStorage.setItem("bugler.test.fruit-sort", '{"column":"retired","desc":false}');

  renderTable("bugler.test.fruit-sort");

  expect(names()).toEqual(["banana", "apple", "cherry"]);
});
