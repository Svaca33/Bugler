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

  test("credits the kind of trouble, not the episode, and names what it replaces", () => {
    const line = describeQuietWindow({ own: 120, inherited: 15 });

    expect(line).toContain("2 h for this kind of trouble in this service");
    expect(line).toContain("inherit 15 min");
    expect(line).not.toContain("episode");
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
