import { expect, test } from "bun:test";

import { parseRetentionInput, shortensRetention } from "./retentionChange";

const DEFAULT_DAYS = 30;

const cases: Array<{
  what: string;
  current: number | null;
  next: number | null;
  shortens: boolean;
}> = [
  { what: "a smaller override", current: 90, next: 30, shortens: true },
  { what: "dropping an override above the default", current: 90, next: null, shortens: true },
  { what: "dropping an override below the default", current: 7, next: null, shortens: false },
  { what: "overriding the default downwards", current: null, next: 7, shortens: true },
  { what: "a larger override", current: 30, next: 60, shortens: false },
  { what: "the same override again", current: 30, next: 30, shortens: false },
  { what: "an override that restates the default", current: null, next: 30, shortens: false },
  { what: "leaving the default alone", current: null, next: null, shortens: false },
];

for (const { what, current, next, shortens } of cases) {
  test(`${what} ${shortens ? "shortens" : "does not shorten"} retention`, () => {
    expect(shortensRetention(current, next, DEFAULT_DAYS)).toBe(shortens);
  });
}

test("an empty field means the server default", () => {
  expect(parseRetentionInput("")).toEqual({ valid: true, days: null });
  expect(parseRetentionInput("   ")).toEqual({ valid: true, days: null });
});

test("whole days are a policy, surrounding space forgiven", () => {
  expect(parseRetentionInput("14")).toEqual({ valid: true, days: 14 });
  expect(parseRetentionInput(" 7 ")).toEqual({ valid: true, days: 7 });
  expect(parseRetentionInput("1")).toEqual({ valid: true, days: 1 });
});

test("anything short of a whole day is not a policy", () => {
  for (const raw of ["0", "-5", "1.5", "abc", "1e-3"]) {
    expect(parseRetentionInput(raw)).toEqual({ valid: false });
  }
});
