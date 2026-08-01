import { test, expect } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";

import { EpisodesHelpDialog } from "./EpisodesHelpDialog";

function openDialog() {
  render(<EpisodesHelpDialog />);
  fireEvent.click(screen.getByRole("button", { name: "How episodes work" }));
  return screen.getByRole("dialog", { name: "How episodes work" });
}

test("the ? opens the dialog labelled by its title", () => {
  const dialog = openDialog();

  expect(dialog).toBeTruthy();
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

test("each of the four states appears exactly once", () => {
  const dialog = openDialog();

  for (const state of ["open", "quieted", "solved", "muted"]) {
    const badges = Array.from(dialog.querySelectorAll("span")).filter(
      span => span.textContent === state,
    );
    expect(badges.length).toBe(1);
  }
});
