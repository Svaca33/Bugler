import { expect, test } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";

import { DeleteKindDialog } from "./DeleteKindDialog";

function renderDialog(overrides: Partial<Parameters<typeof DeleteKindDialog>[0]> = {}) {
  const calls = { confirmed: 0, openChanges: [] as boolean[] };
  render(
    <DeleteKindDialog
      open
      onOpenChange={open => calls.openChanges.push(open)}
      episodeCount={3}
      pending={false}
      failure={null}
      onConfirm={() => {
        calls.confirmed += 1;
      }}
      {...overrides}
    />,
  );
  return calls;
}

const deleteButton = () =>
  screen.getByRole("button", { name: "Delete for good" }) as HTMLButtonElement;

test("names what is about to be lost, the whole kind included", () => {
  renderDialog();

  expect(screen.getByText("Delete this kind of trouble for good?")).toBeTruthy();
  expect(screen.getByText(/3 in all/)).toBeTruthy();
  expect(screen.getByText(/This cannot be undone\./)).toBeTruthy();
});

test("is disarmed until the phrase is typed, and a stray click does nothing", () => {
  const calls = renderDialog();

  expect(deleteButton().disabled).toBe(true);
  fireEvent.click(deleteButton());
  expect(calls.confirmed).toBe(0);

  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: "del" } });
  expect(deleteButton().disabled).toBe(true);
});

test("arms once the phrase matches and confirms on submit", () => {
  const calls = renderDialog();

  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: " delete " } });
  expect(deleteButton().disabled).toBe(false);

  fireEvent.click(deleteButton());
  expect(calls.confirmed).toBe(1);
});

test("stays disarmed while the deletion is in flight", () => {
  renderDialog({ pending: true });

  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: "delete" } });
  expect(deleteButton().disabled).toBe(true);
});

test("shows the server's refusal verbatim", () => {
  renderDialog({ failure: new Error("Every Episode of the kind must be archived first.") });

  expect(screen.getByText("Every Episode of the kind must be archived first.")).toBeTruthy();
});
