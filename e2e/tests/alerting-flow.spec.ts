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

  // The row deep-links to the logs view with the first record already open in the panel.
  await newest.click();
  await expect(page.getByRole("heading", { name: "Log record", exact: true })).toBeVisible();
  await expect(
    page.getByRole("complementary").getByText("Payment declined: insufficient funds"),
  ).toBeVisible();
});
