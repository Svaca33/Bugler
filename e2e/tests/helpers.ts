import { expect, type Page } from "@playwright/test";

export const ADMIN_EMAIL = "admin@bugler.local";
export const ADMIN_PASSWORD = "LocalAdmin123!";

/** Signs in as the local admin, creating the account on a first run. */
export async function signIn(page: Page) {
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

/** Drives a Radix Select in a filter bar: open by placeholder, pick by option label. */
export async function selectFilter(page: Page, placeholder: string, optionLabel: string) {
  await page.getByRole("combobox").filter({ hasText: placeholder }).click();
  await page.getByRole("option", { name: optionLabel }).click();
}

/** Registers an application with one service through the admin UI and returns its API key. */
export async function registerApplication(page: Page, appName: string) {
  // Exact: the header carries the signed-in account's own address beside the tabs, and a
  // substring match on "Admin" claims admin@bugler.local as well.
  await page.getByRole("link", { name: "Admin", exact: true }).click();
  await page.getByLabel("Add application").fill(appName);
  await page.getByRole("button", { name: "Add", exact: true }).click();
  await expect(page.getByRole("button", { name: appName })).toBeVisible();

  // Exact: a label is matched as a substring by default, and the grouping card on this same
  // page carries "Environment must match" and "Service name must match" (ADR 0034).
  await page.getByLabel("Namespace (deployment)").fill("e2e");
  await page.getByLabel("Environment", { exact: true }).fill("prod");
  await page.getByLabel("Service name", { exact: true }).fill("backend");
  await page.getByRole("button", { name: "Add service" }).click();
  await expect(page.getByText("e2e/prod · backend", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Issue key" }).click();
  const apiKey = (await page.getByTestId("issued-key").textContent())?.trim();
  expect(apiKey).toMatch(/^blgr_/);

  // A newly registered application is inside nobody's Focus, so nothing it sends would show up
  // anywhere until somebody says they are watching it — which is what a person does next too.
  await watchApplication(page, appName);
  return apiKey!;
}

/** Ticks one application in the signed-in account's Focus, through the card a person uses. */
export async function watchApplication(page: Page, appName: string) {
  await page.getByRole("link", { name: ADMIN_EMAIL }).click();
  const box = page.getByLabel(appName, { exact: true });
  await expect(box).toBeVisible();
  if (!(await box.isChecked())) {
    await box.click();
    await expect(box).toBeChecked();
  }
}
