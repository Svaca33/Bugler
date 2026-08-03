import { describe, expect, test } from "bun:test";

import { sanitizeRedirectTarget } from "./redirectTarget";

describe("sanitizeRedirectTarget", () => {
  test("keeps an address inside the app, search and all", () => {
    expect(sanitizeRedirectTarget("/episodes?episode=0199c0de-1a2b-7c3d-8e4f-5a6b7c8d9e0f"))
      .toBe("/episodes?episode=0199c0de-1a2b-7c3d-8e4f-5a6b7c8d9e0f");
    expect(sanitizeRedirectTarget("/traces/4bf92f3577b34da6a3ce929d0e0e4736"))
      .toBe("/traces/4bf92f3577b34da6a3ce929d0e0e4736");
    expect(sanitizeRedirectTarget("/?severity=Error&q=timeout"))
      .toBe("/?severity=Error&q=timeout");
    expect(sanitizeRedirectTarget("/")).toBe("/");
  });

  test("refuses an address that would leave this origin", () => {
    expect(sanitizeRedirectTarget("https://elsewhere.example/episodes")).toBeUndefined();
    expect(sanitizeRedirectTarget("//elsewhere.example")).toBeUndefined();
    expect(sanitizeRedirectTarget("/\\elsewhere.example")).toBeUndefined();
    expect(sanitizeRedirectTarget("javascript:alert(1)")).toBeUndefined();
  });

  test("refuses one that leaves only once a browser has stripped it", () => {
    // A tab or newline inside "//host" is dropped before the address is resolved, so a rule that
    // read the text rather than resolving it would wave these through.
    expect(sanitizeRedirectTarget("/\t/elsewhere.example")).toBeUndefined();
    expect(sanitizeRedirectTarget("/\n/elsewhere.example")).toBeUndefined();
    expect(sanitizeRedirectTarget("/\r/elsewhere.example")).toBeUndefined();
  });

  test("refuses anything that is not a path at all", () => {
    expect(sanitizeRedirectTarget("")).toBeUndefined();
    expect(sanitizeRedirectTarget("dashboard")).toBeUndefined();
    expect(sanitizeRedirectTarget(undefined)).toBeUndefined();
    expect(sanitizeRedirectTarget(null)).toBeUndefined();
    expect(sanitizeRedirectTarget(42)).toBeUndefined();
    expect(sanitizeRedirectTarget(["/episodes"])).toBeUndefined();
  });
});
