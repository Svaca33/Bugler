import { expect, test } from "bun:test";
import { fireEvent, render, screen } from "@testing-library/react";

import { TimeFilterControl } from "./TimeFilterControl";
import type { TimeFilterValue } from "./timeFilter";

test("the trigger names the window the list is showing", () => {
  render(<TimeFilterControl value={{ range: "PT1H" }} onChange={() => {}} />);

  expect(screen.getByLabelText("Time range").textContent).toContain("Last 1 h");
});

test("a custom amount becomes a duration the server can resolve", () => {
  let applied: TimeFilterValue | undefined;
  render(<TimeFilterControl value={{ range: "PT45M" }} onChange={next => (applied = next)} />);

  fireEvent.click(screen.getByRole("button", { name: "Edit" }));
  fireEvent.change(screen.getByLabelText("Amount"), { target: { value: "90" } });
  fireEvent.click(screen.getByRole("button", { name: "Apply relative range" }));

  expect(applied).toEqual({ range: "PT90M" });
});

test("custom ends are typed in local time and leave as instants", () => {
  let applied: TimeFilterValue | undefined;
  render(<TimeFilterControl value={{ range: "PT45M" }} onChange={next => (applied = next)} />);

  fireEvent.click(screen.getByRole("button", { name: "Edit" }));
  fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-07-28T09:00:00" } });
  fireEvent.change(screen.getByLabelText("To"), { target: { value: "2026-07-28T18:00:00" } });
  fireEvent.click(screen.getByRole("button", { name: "Apply absolute range" }));

  expect(applied).toEqual({
    from: new Date("2026-07-28T09:00:00").toISOString(),
    to: new Date("2026-07-28T18:00:00").toISOString(),
  });
});

test("either end may be left open", () => {
  let applied: TimeFilterValue | undefined;
  render(<TimeFilterControl value={{ range: "PT45M" }} onChange={next => (applied = next)} />);

  fireEvent.click(screen.getByRole("button", { name: "Edit" }));
  fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-07-28T09:00:00" } });
  fireEvent.click(screen.getByRole("button", { name: "Apply absolute range" }));

  expect(applied).toEqual({ from: new Date("2026-07-28T09:00:00").toISOString(), to: undefined });
});

test("a range that ends before it starts is never sent", () => {
  render(<TimeFilterControl value={{ range: "PT45M" }} onChange={() => {}} />);

  fireEvent.click(screen.getByRole("button", { name: "Edit" }));
  fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-07-28T18:00:00" } });
  fireEvent.change(screen.getByLabelText("To"), { target: { value: "2026-07-28T09:00:00" } });

  expect(screen.getByRole("button", { name: "Apply absolute range" }).hasAttribute("disabled")).toBe(true);
  expect(screen.getByText("This range ends before it starts.")).toBeTruthy();
});
