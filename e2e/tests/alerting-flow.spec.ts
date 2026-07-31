import { execSync } from "node:child_process";
import { expect, test } from "@playwright/test";

import { registerApplication, signIn } from "./helpers";

/**
 * Alerting end to end: subscribe to a fresh application, export telemetry with an error in it,
 * watch the detection loop open an Episode on the Alerts tab, and follow the deep link to the
 * exact Log Record that opened it.
 */
test("an error log opens an episode that deep-links to its first record", async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page);

  const appName = `Alerting E2E ${Date.now()}`;
  const apiKey = await registerApplication(page, appName);

  // Subscribe to the whole application — services present and future.
  await page.getByRole("link", { name: "Alerts" }).click();
  await page.getByRole("button", { name: "Subscriptions" }).click();
  await page.getByLabel(appName, { exact: true }).click();
  await expect(page.getByLabel(appName, { exact: true })).toBeChecked();

  // Export telemetry containing an error log; the detection loop polls every 15 s.
  execSync(`dotnet run tools/send-sample-telemetry.cs -- ${apiKey}`, {
    cwd: "..",
    encoding: "utf8",
  });

  // Episodes list newest first; earlier runs may have left older episodes behind.
  await page.getByRole("button", { name: "Episodes" }).click();
  const rows = page.getByTestId("episode-rows");
  const newest = rows.getByText("Payment declined: insufficient funds").first();
  await expect(newest).toBeVisible({ timeout: 60_000 });

  // Expand the row: the two human hands live in the detail (see CONTEXT.md: Acknowledged, Solved).
  const row = rows
    .getByTestId("episode-row")
    .filter({ hasText: "Payment declined: insufficient funds" })
    .first();
  await row.getByRole("button", { name: "Expand episode" }).click();
  await expect(page.getByText("Nobody is on this yet.")).toBeVisible();

  // Acknowledge — the mark names its holder.
  await page.getByRole("button", { name: "Acknowledge", exact: true }).click();
  await expect(page.getByText(/Acknowledged by/)).toBeVisible();

  // Solve — the verdict closes the open Episode and consumes the acknowledgement.
  await page.getByRole("button", { name: "Solve", exact: true }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Solve" }).click();
  await expect(page.getByText(/Solved by/)).toBeVisible();
  await expect(row.getByText("solved", { exact: true })).toBeVisible();

  // The row deep-links to the logs view with the first record already open in the panel.
  await newest.click();
  await expect(page.getByRole("heading", { name: "Log record", exact: true })).toBeVisible();
  await expect(
    page.getByRole("complementary").getByText("Payment declined: insufficient funds"),
  ).toBeVisible();
});
