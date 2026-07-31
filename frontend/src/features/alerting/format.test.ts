import { describe, expect, test } from "bun:test";

import { dayLabel, describeLiveMillis, ordinal } from "./format";

describe("describeLiveMillis", () => {
  test("bare seconds under a minute", () => {
    expect(describeLiveMillis(0)).toBe("0 s");
    expect(describeLiveMillis(36_000)).toBe("36 s");
    expect(describeLiveMillis(-500)).toBe("0 s");
  });

  test("minutes carry padded seconds under an hour", () => {
    expect(describeLiveMillis(61_000)).toBe("1 min 01 s");
    expect(describeLiveMillis(22 * 60_000 + 36_000)).toBe("22 min 36 s");
  });

  test("hours carry padded minutes and drop the seconds", () => {
    expect(describeLiveMillis(60 * 60_000)).toBe("1 h 00 min");
    expect(describeLiveMillis(3 * 3_600_000 + 5 * 60_000 + 59_000)).toBe("3 h 05 min");
  });
});

describe("ordinal", () => {
  test("speaks English", () => {
    expect(ordinal(1)).toBe("1st");
    expect(ordinal(2)).toBe("2nd");
    expect(ordinal(3)).toBe("3rd");
    expect(ordinal(4)).toBe("4th");
    expect(ordinal(11)).toBe("11th");
    expect(ordinal(12)).toBe("12th");
    expect(ordinal(13)).toBe("13th");
    expect(ordinal(22)).toBe("22nd");
  });
});

describe("dayLabel", () => {
  const noon = new Date(2026, 6, 31, 12, 0, 0).getTime();

  test("today and yesterday are named, older days wear their weekday", () => {
    expect(dayLabel(new Date(2026, 6, 31, 9, 0, 0).toISOString(), noon)).toBe("TODAY · 31 JUL");
    expect(dayLabel(new Date(2026, 6, 30, 9, 0, 0).toISOString(), noon)).toBe("YESTERDAY · 30 JUL");
    expect(dayLabel(new Date(2026, 6, 29, 9, 0, 0).toISOString(), noon)).toBe("WED · 29 JUL");
  });
});
