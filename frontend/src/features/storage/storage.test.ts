import { expect, test } from "bun:test";

import { formatBytes } from "@/lib/format";

import { ratePer, settledBytes, totalFootprint, type StorageRow } from "./storage";

const DAY_MS = 24 * 60 * 60 * 1000;

function row(overrides?: Partial<StorageRow>): StorageRow {
  return {
    serviceId: "a",
    logs: { footprintBytes: 0, retentionDays: 7, windowBytes: 0 },
    traces: { footprintBytes: 0, retentionDays: 3, windowBytes: 0 },
    ...overrides,
  };
}

test("bytes read like pg_size_pretty: 1024 steps, precision by magnitude", () => {
  expect(formatBytes(0)).toBe("0 B");
  expect(formatBytes(-5)).toBe("0 B");
  expect(formatBytes(512)).toBe("512 B");
  expect(formatBytes(1024)).toBe("1.00 kB");
  expect(formatBytes(15.6 * 1024)).toBe("15.6 kB");
  expect(formatBytes(347 * 1024)).toBe("347 kB");
  expect(formatBytes(1024 * 1024 * 1024 * 1.5)).toBe("1.50 GB");
});

test("the rate is the window's bytes rescaled to the asked-for unit", () => {
  expect(ratePer(DAY_MS, DAY_MS, "second")).toBe(1000);
  expect(ratePer(2400, DAY_MS, "hour")).toBe(100);
  expect(ratePer(2400, DAY_MS, "day")).toBe(2400);
  expect(ratePer(2400, DAY_MS, "week")).toBe(16_800);
  expect(ratePer(2400, DAY_MS, "month")).toBe(72_000);
  expect(ratePer(2400, 0, "day")).toBe(0);
});

test("the Settled Footprint holds each kind for its own retention", () => {
  const service = row({
    logs: { footprintBytes: 0, retentionDays: 7, windowBytes: 700 },
    traces: { footprintBytes: 0, retentionDays: 3, windowBytes: 300 },
  });

  // A window exactly one day long: 700 B/day held 7 days + 300 B/day held 3 days.
  expect(settledBytes(service, DAY_MS)).toBe(700 * 7 + 300 * 3);
});

test("the Total Footprint is both kinds together", () => {
  const service = row({
    logs: { footprintBytes: 5, retentionDays: 7, windowBytes: 0 },
    traces: { footprintBytes: 100, retentionDays: 3, windowBytes: 0 },
  });

  expect(totalFootprint(service)).toBe(105);
});
