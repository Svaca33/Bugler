import { expect, test } from "bun:test";

import { archiveMany, isSelectable } from "./bulkArchive";

test("every id is tried, and the outcome tells the filed from the refused in selection order", async () => {
  const tried: string[] = [];
  const outcome = await archiveMany(["a", "b", "c"], async id => {
    tried.push(id);
    if (id === "b") throw new Error("Only a closed Episode may be archived.");
  });

  expect(tried).toEqual(["a", "b", "c"]);
  expect(outcome.filed).toEqual(["a", "c"]);
  expect(outcome.refused).toEqual([{ id: "b", reason: "Only a closed Episode may be archived." }]);
});

test("a refusal without words is still a refusal", async () => {
  const outcome = await archiveMany(["a"], async () => {
    throw "boom";
  });

  expect(outcome.filed).toEqual([]);
  expect(outcome.refused).toEqual([{ id: "a", reason: "" }]);
});

test("nothing selected is nothing done", async () => {
  let calls = 0;
  const outcome = await archiveMany([], async () => {
    calls += 1;
  });

  expect(calls).toBe(0);
  expect(outcome).toEqual({ filed: [], refused: [] });
});

test("only an Episode not yet filed can join the selection", () => {
  expect(isSelectable({ archivedAt: null })).toBe(true);
  expect(isSelectable({ archivedAt: "2026-09-05T10:00:00Z" })).toBe(false);
});
