import { expect, test } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";

import { DeleteConfirmation } from "./DeleteConfirmation";

function renderDialog(overrides: Partial<Parameters<typeof DeleteConfirmation>[0]> = {}) {
  const calls = { confirmed: 0, openChanges: [] as boolean[] };
  render(
    <DeleteConfirmation
      inputId="svc-1"
      open
      onOpenChange={open => calls.openChanges.push(open)}
      title='Delete service "demo/prod · backend"?'
      consequence="This erases every log and span it sent."
      phrase="demo/prod/backend"
      pending={false}
      failed={false}
      onConfirm={() => {
        calls.confirmed += 1;
      }}
      {...overrides}
    />,
  );
  return calls;
}

const deleteButton = () => screen.getByRole("button", { name: "Delete" }) as HTMLButtonElement;

test("names what is about to be lost", () => {
  renderDialog();

  expect(screen.getByText('Delete service "demo/prod · backend"?')).toBeTruthy();
  expect(screen.getByText(/This cannot be undone\./)).toBeTruthy();
});

test("is disarmed until the phrase is typed", () => {
  renderDialog();

  expect(deleteButton().disabled).toBe(true);

  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: "backend" } });
  expect(deleteButton().disabled).toBe(true);
});

test("arms once the phrase matches and confirms on submit", () => {
  const calls = renderDialog();

  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: "demo/prod/backend" } });
  expect(deleteButton().disabled).toBe(false);

  fireEvent.click(deleteButton());
  expect(calls.confirmed).toBe(1);
});

test("stays disarmed while the deletion is in flight", () => {
  renderDialog({ pending: true });

  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: "demo/prod/backend" } });
  expect(deleteButton().disabled).toBe(true);
});

test("cancelling closes without confirming", () => {
  const calls = renderDialog();

  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: "demo/prod/backend" } });
  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

  expect(calls.confirmed).toBe(0);
  expect(calls.openChanges).toEqual([false]);
});

test("reports a failed deletion without claiming anything was removed", () => {
  renderDialog({ failed: true });

  expect(screen.getByText("Deletion failed — nothing was removed.")).toBeTruthy();
});

test("renders nothing while closed", () => {
  renderDialog({ open: false });

  expect(screen.queryByRole("button", { name: "Delete" })).toBeNull();
});
