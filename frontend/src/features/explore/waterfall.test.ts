import { expect, test } from "bun:test";

import { layoutWaterfall } from "./waterfall";

const span = (spanId: string, parentSpanId: string | null, startMs: number, endMs: number) => ({
  spanId,
  parentSpanId,
  startTime: new Date(1_700_000_000_000 + startMs).toISOString(),
  endTime: new Date(1_700_000_000_000 + endMs).toISOString(),
});

test("children are indented under their parent in start order", () => {
  const rows = layoutWaterfall([
    span("root", null, 0, 100),
    span("late-child", "root", 60, 90),
    span("early-child", "root", 10, 40),
  ]);

  expect(rows.map(r => r.span.spanId)).toEqual(["root", "early-child", "late-child"]);
  expect(rows.map(r => r.depth)).toEqual([0, 1, 1]);
});

test("bars are positioned on the shared trace timeline", () => {
  const rows = layoutWaterfall([span("root", null, 0, 200), span("child", "root", 50, 150)]);

  expect(rows[0]!.offsetPercent).toBe(0);
  expect(rows[0]!.widthPercent).toBe(100);
  expect(rows[1]!.offsetPercent).toBe(25);
  expect(rows[1]!.widthPercent).toBe(50);
  expect(rows[1]!.durationMs).toBe(100);
});

test("orphaned spans surface at depth zero instead of disappearing", () => {
  const rows = layoutWaterfall([span("root", null, 0, 100), span("orphan", "missing-parent", 20, 80)]);

  expect(rows).toHaveLength(2);
  expect(rows.find(r => r.span.spanId === "orphan")?.depth).toBe(0);
});

test("empty input yields empty layout", () => {
  expect(layoutWaterfall([])).toEqual([]);
});
