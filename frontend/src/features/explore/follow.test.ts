import { expect, test } from "bun:test";

import type { LogRecord } from "@/api/client";

import { FOLLOW_CAP, followCursor, mergeFollowed } from "./follow";

const record = (id: number, second = id): LogRecord =>
  ({
    id,
    serviceId: "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
    timestamp: `2026-08-02T14:00:${String(second).padStart(2, "0")}.000Z`,
    observedTimestamp: null,
    severityNumber: 9,
    severityText: "INFO",
    body: `record ${id}`,
    traceId: null,
    spanId: null,
    scopeName: null,
    resourceAttributes: {},
    attributes: {},
  }) as unknown as LogRecord;

const page = (from: number, count: number) =>
  Array.from({ length: count }, (_, index) => record(from - index));

test("the cursor names the newest record held", () => {
  const cursor = followCursor([record(9), record(8), record(7)]);

  expect(cursor).toEqual({ after: "2026-08-02T14:00:09.000Z", afterId: 9 });
});

test("an empty list has nowhere to read forward from", () => {
  expect(followCursor([])).toBeUndefined();
});

test("what arrived goes on top, newest first", () => {
  const merged = mergeFollowed([record(2), record(1)], [record(4), record(3)]);

  expect(merged.map(item => item.id)).toEqual([4, 3, 2, 1]);
});

test("a record already held is not shown twice", () => {
  // The one tick that can overlap: the first, when Follow starts with no cursor to read from.
  const merged = mergeFollowed([record(3), record(2), record(1)], [record(4), record(3)]);

  expect(merged.map(item => item.id)).toEqual([4, 3, 2, 1]);
});

test("nothing new leaves the list as it stood", () => {
  const held = [record(2), record(1)];

  expect(mergeFollowed(held, [])).toEqual(held);
});

test("past the cap the oldest end falls off, never the newest", () => {
  const held = page(FOLLOW_CAP, FOLLOW_CAP);

  const merged = mergeFollowed(held, page(FOLLOW_CAP + 3, 3));

  expect(merged).toHaveLength(FOLLOW_CAP);
  expect(merged[0]!.id).toBe(FOLLOW_CAP + 3);
  expect(merged.at(-1)!.id).toBe(4);
});
