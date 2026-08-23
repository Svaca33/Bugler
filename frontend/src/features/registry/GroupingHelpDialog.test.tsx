import { test, expect } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";

import { GroupingHelpDialog } from "./GroupingHelpDialog";

function openDialog() {
  render(<GroupingHelpDialog />);
  fireEvent.click(screen.getByRole("button", { name: "How grouping works" }));
  return screen.getByRole("dialog", { name: "How grouping works" });
}

test("the ? opens the dialog labelled by its title", () => {
  expect(openDialog()).toBeTruthy();
});

test("Escape closes the dialog", () => {
  const dialog = openDialog();

  fireEvent.keyDown(dialog, { key: "Escape" });

  expect(screen.queryByRole("dialog")).toBeNull();
});

test("Got it closes the dialog", () => {
  openDialog();

  fireEvent.click(screen.getByRole("button", { name: "Got it" }));

  expect(screen.queryByRole("dialog")).toBeNull();
});

test("every rung of the ladder is named, in the order the recipe tries them", () => {
  const dialog = openDialog();

  // The dropdown offers three of them and the attribute outranks all three; a reader who cannot
  // see the fourth cannot tell what "group by" is choosing between.
  const rungs = [
    "A named attribute",
    "The code that threw",
    "The kind of failure",
    "What was said",
  ];
  const positions = rungs.map(rung => dialog.textContent!.indexOf(rung));

  expect(positions.every(at => at >= 0)).toBe(true);
  expect(positions).toEqual([...positions].sort((a, b) => a - b));
});

test("the stack sample shows what is dropped and what survives", () => {
  const dialog = openDialog();
  const [raw, kept] = Array.from(dialog.querySelectorAll("pre")).map(node => node.textContent!);

  // The header carries a hostname and a transaction number — the whole reason frames are hashed
  // and words are not — so it has to be visible on the left and gone on the right.
  expect(raw).toContain("db-07");
  expect(raw).toContain("Payments.cs:line 42");
  expect(kept).not.toContain("db-07");
  expect(kept).not.toContain(":line");
  expect(kept).toContain("Acme.Payments.Charge(Order o)");
});
