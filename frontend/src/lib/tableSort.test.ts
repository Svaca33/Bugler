import { expect, test } from "bun:test";

import { readTableSort, writeTableSort, type TableSort } from "./tableSort";

const COLUMNS = ["service", "total"];

function memory(entries?: Record<string, string>) {
  const map = new Map(Object.entries(entries ?? {}));
  return {
    getItem: (key: string) => map.get(key) ?? null,
    setItem: (key: string, value: string) => void map.set(key, value),
    map,
  };
}

test("a written sort round-trips", () => {
  const storage = memory();
  const sort: TableSort = { column: "total", desc: true };

  writeTableSort("bugler.test.sort", sort, storage);

  expect(readTableSort("bugler.test.sort", COLUMNS, storage)).toEqual(sort);
});

test("nothing stored means nothing restored", () => {
  expect(readTableSort("bugler.test.sort", COLUMNS, memory())).toBeUndefined();
  expect(readTableSort("bugler.test.sort", COLUMNS, null)).toBeUndefined();
});

test("malformed entries are dropped, never guessed", () => {
  const cases = [
    "not json",
    "42",
    "null",
    '{"column":7,"desc":true}',
    '{"column":"total"}',
    '{"desc":false}',
  ];
  for (const raw of cases) {
    const storage = memory({ "bugler.test.sort": raw });
    expect(readTableSort("bugler.test.sort", COLUMNS, storage)).toBeUndefined();
  }
});

test("a column the table no longer sorts by falls back to the default", () => {
  const storage = memory({ "bugler.test.sort": '{"column":"retired","desc":true}' });

  expect(readTableSort("bugler.test.sort", COLUMNS, storage)).toBeUndefined();
});

test("a refusing storage loses the preference, not the page", () => {
  const throwing = {
    getItem: () => {
      throw new Error("denied");
    },
    setItem: () => {
      throw new Error("full");
    },
  };

  expect(() => writeTableSort("bugler.test.sort", { column: "total", desc: true }, throwing)).not.toThrow();
  expect(readTableSort("bugler.test.sort", COLUMNS, throwing)).toBeUndefined();
});
