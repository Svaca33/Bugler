import { describe, expect, test } from "bun:test";

import { confirmationMatches, serviceConfirmationPhrase } from "./deletionConfirmation";

describe("serviceConfirmationPhrase", () => {
  test("is the three registered facets, typeable", () => {
    expect(serviceConfirmationPhrase({ namespace: "demo", environment: "prod", name: "backend" })).toBe(
      "demo/prod/backend",
    );
  });

  test("distinguishes services that differ only by deployment", () => {
    const prod = serviceConfirmationPhrase({ namespace: "demo", environment: "prod", name: "backend" });
    const staging = serviceConfirmationPhrase({ namespace: "demo", environment: "staging", name: "backend" });
    expect(prod).not.toBe(staging);
  });
});

describe("confirmationMatches", () => {
  test("accepts the exact phrase", () => {
    expect(confirmationMatches("demo/prod/backend", "demo/prod/backend")).toBe(true);
  });

  test("forgives surrounding whitespace", () => {
    expect(confirmationMatches("  demo/prod/backend  ", "demo/prod/backend")).toBe(true);
  });

  test("rejects a different case", () => {
    expect(confirmationMatches("Demo/Prod/Backend", "demo/prod/backend")).toBe(false);
  });

  test("rejects a partial phrase", () => {
    expect(confirmationMatches("backend", "demo/prod/backend")).toBe(false);
  });

  test("rejects the empty input", () => {
    expect(confirmationMatches("", "demo/prod/backend")).toBe(false);
    expect(confirmationMatches("   ", "demo/prod/backend")).toBe(false);
  });

  test("never authorises on an empty expected phrase", () => {
    expect(confirmationMatches("", "")).toBe(false);
  });
});
