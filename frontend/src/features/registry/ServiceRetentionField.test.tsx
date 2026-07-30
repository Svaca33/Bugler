import { expect, test } from "bun:test";
import { act, fireEvent, render, screen } from "@testing-library/react";

import { RetentionField } from "./ServiceRetentionField";

/** Renders the field over a stored 90-day override against a 30-day server default. */
function renderField(overrides: Partial<Parameters<typeof RetentionField>[0]> = {}) {
  const saved: Array<number | null> = [];
  render(
    <RetentionField
      id="svc-1"
      value={90}
      defaultRetentionDays={30}
      pending={false}
      error={null}
      {...overrides}
      onSave={days => {
        saved.push(days);
        return Promise.resolve();
      }}
    />,
  );
  return saved;
}

const field = () => screen.getByLabelText("Retention (days)") as HTMLInputElement;

const commit = (value: string) => {
  fireEvent.change(field(), { target: { value } });
  fireEvent.blur(field());
};

const dialog = () => screen.queryByRole("button", { name: "Shorten retention" });

test("lengthening saves without asking", () => {
  const saved = renderField();

  commit("120");

  expect(saved).toEqual([120]);
  expect(dialog()).toBeNull();
});

test("shortening asks before it saves anything", () => {
  const saved = renderField();

  commit("30");

  expect(saved).toEqual([]);
  expect(dialog()).not.toBeNull();
  expect(screen.getByText("Shorten retention to 30 days?")).toBeTruthy();
});

test("confirming the shortening saves it and closes", async () => {
  const saved = renderField();

  commit("30");
  // The dialog closes only once the save has resolved, so let the microtasks run.
  await act(async () => {
    fireEvent.click(dialog()!);
  });

  expect(saved).toEqual([30]);
  expect(dialog()).toBeNull();
});

test("cancelling puts the stored value back", () => {
  const saved = renderField();

  commit("7");
  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

  expect(saved).toEqual([]);
  expect(dialog()).toBeNull();
  expect(field().value).toBe("90");
});

test("clearing an override above the default is a shortening too", () => {
  const saved = renderField();

  commit("");

  expect(saved).toEqual([]);
  expect(screen.getByText(/follow the server default of 30 days/)).toBeTruthy();
});

test("clearing an override below the default saves straight away", () => {
  const saved = renderField({ value: 7 });

  commit("");

  expect(saved).toEqual([null]);
  expect(dialog()).toBeNull();
});

test("re-entering the value it already has saves nothing", () => {
  const saved = renderField();

  commit("90");

  expect(saved).toEqual([]);
  expect(dialog()).toBeNull();
});

test("anything short of a whole day is neither saved nor confirmed", () => {
  const saved = renderField();

  for (const raw of ["0", "-5", "1.5"]) {
    commit(raw);
  }

  expect(saved).toEqual([]);
  expect(dialog()).toBeNull();
});
