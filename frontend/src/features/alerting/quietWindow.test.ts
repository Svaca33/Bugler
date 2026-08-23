import { describe, expect, test } from "bun:test";

import { cs } from "@/i18n/cs";
import { en } from "@/i18n/en";
import { activate } from "@/i18n/runtime";

import {
  describeQuietWindow,
  MAX_QUIET_WINDOW_MINUTES,
  quietWindowError,
  quietWindowWords,
} from "./quietWindow";

describe("quietWindowWords", () => {
  test("keeps minutes that are not round hours", () => {
    expect(quietWindowWords(15)).toBe("15 min");
    expect(quietWindowWords(90)).toBe("90 min");
  });

  test("says hours and days where they are exact", () => {
    expect(quietWindowWords(60)).toBe("1 h");
    expect(quietWindowWords(120)).toBe("2 h");
    expect(quietWindowWords(1440)).toBe("1 day");
    expect(quietWindowWords(MAX_QUIET_WINDOW_MINUTES)).toBe("7 days");
  });

  test("Czech days follow the grammar's plural, not an if-chain", () => {
    activate("cs", cs);
    try {
      expect(quietWindowWords(1440)).toBe("1 den");
      expect(quietWindowWords(2880)).toBe("2 dny");
      expect(quietWindowWords(MAX_QUIET_WINDOW_MINUTES)).toBe("7 dní");
    } finally {
      activate("en", en);
    }
  });
});

describe("describeQuietWindow", () => {
  test("names the service as the source while the kind inherits", () => {
    expect(describeQuietWindow({ own: null, inherited: 15 }))
      .toBe("Inherited from the service: 15 min.");
  });

  test("credits the kind of trouble, not one episode, and names what it replaces", () => {
    const line = describeQuietWindow({ own: 120, inherited: 15 });

    // What is tuned is the kind of trouble across its Episode Scope (ADR 0034), so the sentence
    // reaches as far as the episode does — and still never says one episode owns the window.
    expect(line).toContain("2 h for this kind of trouble wherever its episode reaches");
    expect(line).toContain("inherit 15 min");
    expect(line).not.toContain("this episode");
  });
});

describe("quietWindowError", () => {
  test("empty means inherit and is always allowed", () => {
    expect(quietWindowError("")).toBeNull();
    expect(quietWindowError("   ")).toBeNull();
  });

  test("accepts whole minutes inside the bounds", () => {
    expect(quietWindowError("1")).toBeNull();
    expect(quietWindowError("120")).toBeNull();
    expect(quietWindowError(String(MAX_QUIET_WINDOW_MINUTES))).toBeNull();
  });

  test("refuses what the server would refuse", () => {
    expect(quietWindowError("0")).not.toBeNull();
    expect(quietWindowError(String(MAX_QUIET_WINDOW_MINUTES + 1))).not.toBeNull();
    expect(quietWindowError("2.5")).not.toBeNull();
    expect(quietWindowError("soon")).not.toBeNull();
  });
});
