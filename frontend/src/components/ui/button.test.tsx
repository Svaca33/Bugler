import { test, expect } from "bun:test";
import { render, screen } from "@testing-library/react";

import { Button } from "./button";

test("renders its label", () => {
  render(<Button>Save</Button>);

  expect(screen.getByRole("button", { name: "Save" })).toBeTruthy();
});
