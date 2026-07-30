import { execSync } from "node:child_process";
import { expect, test } from "@playwright/test";

import { registerApplication, selectFilter, signIn } from "./helpers";

/**
 * The whole product in one pass: sign in (or first-run setup), register an
 * application and service, issue an API key, export real OTLP telemetry with
 * it, and watch the logs and the trace waterfall appear in the UI.
 */
test("telemetry flows from an issued key to the log and trace viewers", async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page);

  // Register a fresh application + service and issue its key.
  const appName = `E2E ${Date.now()}`;
  const apiKey = await registerApplication(page, appName);

  // Export logs and a trace over OTLP/HTTP using the key issued through the UI.
  const output = execSync(`dotnet run tools/send-sample-telemetry.cs -- ${apiKey}`, {
    cwd: "..",
    encoding: "utf8",
  });
  const traceId = /trace id: ([0-9a-f]{32})/.exec(output)?.[1];
  expect(traceId).toBeDefined();

  // The log viewer shows the exported records for the new application.
  await page.getByRole("navigation").getByRole("link", { name: "Logs" }).click();
  await selectFilter(page, "All applications", appName);
  const rows = page.getByTestId("log-rows");
  await expect(rows.getByText("Payment declined: insufficient funds")).toBeVisible({ timeout: 15_000 });
  await expect(rows.getByText("Order 1042 placed by customer")).toBeVisible();

  // Log detail links to the correlated trace waterfall.
  await rows.getByText("Payment declined: insufficient funds").click();
  await page.getByRole("link", { name: /View trace/ }).click();
  await expect(page.getByTestId("waterfall")).toContainText("POST /checkout");
  await expect(page.getByTestId("waterfall")).toContainText("charge-card");

  // And the trace list flags it as an error.
  await page.getByRole("navigation").getByRole("link", { name: "Traces" }).click();
  await selectFilter(page, "All applications", appName);
  const traceRows = page.getByTestId("trace-rows");
  await expect(traceRows.getByText("POST /checkout")).toBeVisible();
  await expect(traceRows.getByText("ERROR")).toBeVisible();
});
