import { expect, test } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";

import { SelectionBar } from "./SelectionBar";

const noop = () => {};

test("nothing selected offers no bulk action, and no bar at all", () => {
  const { container } = render(
    <SelectionBar count={0} pending={false} outcome={undefined}
      onArchive={noop} onClear={noop} onDismiss={noop} />,
  );

  expect(container.innerHTML).toBe("");
});

test("a selection says how many and offers to archive them", () => {
  let archived = false;
  render(
    <SelectionBar count={3} pending={false} outcome={undefined}
      onArchive={() => { archived = true; }} onClear={noop} onDismiss={noop} />,
  );

  expect(screen.getByText("3 selected")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Archive selected" }));
  expect(archived).toBe(true);
});

test("the outcome tells the filed from the refused, with the server's sentence", () => {
  render(
    <SelectionBar count={1} pending={false}
      outcome={{ filed: 2, refused: 1, reasons: "Only a closed Episode may be archived." }}
      onArchive={noop} onClear={noop} onDismiss={noop} />,
  );

  expect(screen.getByRole("status").textContent).toBe(
    "2 archived, 1 not — still selected. Only a closed Episode may be archived.",
  );
});

test("an outcome outlives an emptied selection until dismissed, but offers no action", () => {
  let dismissed = false;
  render(
    <SelectionBar count={0} pending={false} outcome={{ filed: 4, refused: 0, reasons: "" }}
      onArchive={noop} onClear={noop} onDismiss={() => { dismissed = true; }} />,
  );

  expect(screen.getByRole("status").textContent).toBe("4 archived.");
  expect(screen.queryByRole("button", { name: "Archive selected" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Dismiss" }));
  expect(dismissed).toBe(true);
});
