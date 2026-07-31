import { execSync } from "node:child_process";
import { expect, test } from "@playwright/test";

import { registerApplication, signIn } from "./helpers";

/**
 * Alerting end to end: subscribe to a fresh application, export telemetry with an error in it,
 * watch the detection loop open an Episode on the Alerts tab, work it in the detail panel —
 * acknowledge, solve — and follow the deep link to the exact Log Record that opened it.
 */
test("an error log opens an episode that is worked in the panel and deep-links to its record", async ({ page }) => {
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

  // A row click selects the episode — the detail panel opens, nothing navigates away. The
  // filter rail is an aside too, so the panel is told apart by a section only it renders.
  const row = rows
    .getByTestId("episode-row")
    .filter({ hasText: "Payment declined: insufficient funds" })
    .first();
  await row.click();
  await page.waitForURL(/episode=/);
  const panel = page.getByRole("complementary").filter({ hasText: "VOLUME SO FAR" });
  await expect(panel.getByText("Payment declined: insufficient funds")).toBeVisible();
  await expect(panel.getByText(/Opened by an ERROR log/)).toBeVisible();

  // Acknowledge — the mark names its holder on the lifecycle timeline.
  await panel.getByRole("button", { name: "Acknowledge", exact: true }).click();
  await expect(panel.getByText("You acknowledged it")).toBeVisible();

  // Solve — the verdict closes the open Episode and consumes the acknowledgement. Another
  // worker may be minting identical episodes, so the verdict is read in the panel, never
  // through a text-addressed row that could meanwhile point at somebody else's trouble.
  await panel.getByRole("button", { name: "Solve", exact: true }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Solve" }).click();
  await expect(panel.getByText(/Solved by/)).toBeVisible();

  // The panel deep-links to the logs view with the first record already open.
  await panel.getByRole("button", { name: "Open in logs", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Log record", exact: true })).toBeVisible();
  await expect(
    page.getByRole("complementary").getByText("Payment declined: insufficient funds"),
  ).toBeVisible();
});
