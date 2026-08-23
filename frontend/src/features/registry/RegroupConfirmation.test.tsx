import { test, expect } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";

import { RegroupConfirmation } from "./RegroupConfirmation";

function renderDialog(overrides: Partial<Parameters<typeof RegroupConfirmation>[0]> = {}) {
  const calls = { confirmed: 0, openChanges: [] as boolean[] };
  render(
    <RegroupConfirmation
      open
      onOpenChange={open => calls.openChanges.push(open)}
      cost={{ count: 3, capped: false }}
      pending={false}
      failed={undefined}
      onConfirm={() => calls.confirmed++}
      {...overrides}
    />,
  );
  return calls;
}

test("it asks rather than saves, and says how many episodes it will mute", () => {
  renderDialog();

  const alert = screen.getByRole("alert");
  expect(alert.textContent).toContain("mutes 3 open episodes");
  // The one thing a reader must not miss about an irreversible change.
  expect(alert.textContent).toContain("This cannot be undone.");
});

test("the confirming button is the destructive one", () => {
  renderDialog();

  const confirm = screen.getByRole("button", { name: "Regroup" });
  expect(confirm.className).toContain("destructive");
});

test("it says it is still counting rather than naming a number it does not have", () => {
  renderDialog({ cost: undefined });

  expect(screen.getByRole("alert").textContent).toContain("what this change will cost");
  expect(screen.queryByText(/mutes/)).toBeNull();
});

test("confirming calls back, cancelling closes without confirming", () => {
  const calls = renderDialog();

  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
  expect(calls.confirmed).toBe(0);
  expect(calls.openChanges).toEqual([false]);

  fireEvent.click(screen.getByRole("button", { name: "Regroup" }));
  expect(calls.confirmed).toBe(1);
});

test("a save that failed says so beside the button that would retry it", () => {
  renderDialog({ failed: "Failed to save the grouping settings" });

  expect(screen.getByText("Failed to save the grouping settings")).toBeTruthy();
  // Still armed: the admin meant it the first time, and the refusal may be transient.
  expect(screen.getByRole("button", { name: "Regroup" }).hasAttribute("disabled")).toBe(false);
});

test("a save in flight disarms the button so it cannot land twice", () => {
  renderDialog({ pending: true });

  expect(screen.getByRole("button", { name: "Regroup" }).hasAttribute("disabled")).toBe(true);
});
