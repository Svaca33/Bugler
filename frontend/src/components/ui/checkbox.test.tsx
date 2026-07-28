import { test, expect } from "bun:test";
import { render, screen } from "@testing-library/react";

import { Checkbox } from "./checkbox";

test("renders an accessible checkbox", () => {
  render(<Checkbox aria-label="Stay signed in" />);

  expect(screen.getByRole("checkbox", { name: "Stay signed in" })).toBeTruthy();
});

test("exposes the checked state", () => {
  render(<Checkbox aria-label="Stay signed in" defaultChecked />);

  const checkbox = screen.getByRole("checkbox", { name: "Stay signed in" });
  expect(checkbox.getAttribute("data-state")).toBe("checked");
});
