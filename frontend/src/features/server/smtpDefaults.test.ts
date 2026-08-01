import { expect, test } from "bun:test";

import { defaultPortFor, portAfterSecurityChange } from "./smtpDefaults";

test("each mode has its conventional port", () => {
  expect(defaultPortFor("Automatic")).toBe(587);
  expect(defaultPortFor("StartTls")).toBe(587);
  expect(defaultPortFor("None")).toBe(25);
  expect(defaultPortFor("ImplicitTls")).toBe(465);
});

test("an untouched port follows the security mode", () => {
  expect(portAfterSecurityChange(587, "Automatic", "None")).toBe(25);
  expect(portAfterSecurityChange(25, "None", "ImplicitTls")).toBe(465);
  expect(portAfterSecurityChange(465, "ImplicitTls", "Automatic")).toBe(587);
});

test("a port somebody typed stays put", () => {
  expect(portAfterSecurityChange(1025, "Automatic", "None")).toBe(1025);
  expect(portAfterSecurityChange(2525, "None", "StartTls")).toBe(2525);
});

test("modes sharing a port change nothing between themselves", () => {
  expect(portAfterSecurityChange(587, "Automatic", "StartTls")).toBe(587);
});
