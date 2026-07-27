import { execSync } from "node:child_process";
import { expect, test, type Page } from "@playwright/test";

const ADMIN_EMAIL = "admin@bugler.local";
const ADMIN_PASSWORD = "LocalAdmin123!";

/**
 * The whole product in one pass: sign in (or first-run setup), register an
 * application and instance, issue an API key, export real OTLP telemetry with
 * it, and watch the logs and the trace waterfall appear in the UI.
 */
test("telemetry flows from an issued key to the log and trace viewers", async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page);

  // Register a fresh application + instance and issue its key.
  const appName = `E2E ${Date.now()}`;
  await page.getByRole("link", { name: "Admin" }).click();
  await page.getByPlaceholder("New application…").fill(appName);
  await page.getByRole("button", { name: "Add", exact: true }).click();
  await expect(page.getByRole("button", { name: appName })).toBeVisible();

  await page.getByPlaceholder("New instance (client)…").fill("e2e-client");
  await page.getByRole("button", { name: "Add instance" }).click();
  await expect(page.getByRole("cell", { name: "e2e-client" })).toBeVisible();

  await page.getByRole("button", { name: "Issue key" }).click();
  const apiKey = (await page.getByTestId("issued-key").textContent())?.trim();
  expect(apiKey).toMatch(/^blgr_/);

  // Export logs and a trace over OTLP/HTTP using the key issued through the UI.
  const output = execSync(`dotnet run tools/send-sample-telemetry.cs -- ${apiKey}`, {
    cwd: "..",
    encoding: "utf8",
  });
  const traceId = /trace id: ([0-9a-f]{32})/.exec(output)?.[1];
  expect(traceId).toBeDefined();

  // The log viewer shows the exported records for the new application.
  await page.getByRole("link", { name: "Logs" }).click();
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
  await page.getByRole("link", { name: "Traces" }).click();
  await selectFilter(page, "All applications", appName);
  const traceRows = page.getByTestId("trace-rows");
  await expect(traceRows.getByText("POST /checkout")).toBeVisible();
  await expect(traceRows.getByText("ERROR")).toBeVisible();
});

async function signIn(page: Page) {
  await page.goto("/login");
  const setupHeading = page.getByText("Welcome to Bugler");
  const loginHeading = page.getByText("Sign in to Bugler");
  await expect(setupHeading.or(loginHeading).first()).toBeVisible();

  if (await setupHeading.isVisible()) {
    await page.getByLabel("Name").fill("E2E Admin");
    await page.getByLabel("E-mail").fill(ADMIN_EMAIL);
    await page.getByLabel("Password (min 8 characters)").fill(ADMIN_PASSWORD);
    await page.getByRole("button", { name: "Create admin account" }).click();
  } else {
    await page.getByLabel("E-mail").fill(ADMIN_EMAIL);
    await page.getByLabel("Password").fill(ADMIN_PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();
  }

  await expect(page.getByRole("link", { name: "Logs" })).toBeVisible();
}

async function selectFilter(page: Page, placeholder: string, optionLabel: string) {
  await page.getByRole("combobox").filter({ hasText: placeholder }).click();
  await page.getByRole("option", { name: optionLabel }).click();
}
