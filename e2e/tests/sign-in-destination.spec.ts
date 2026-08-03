import { expect, test, type Page } from "@playwright/test";

import { ADMIN_EMAIL, ADMIN_PASSWORD, signIn } from "./helpers";

/**
 * The "Open episode" button in an alert — the same address in the mail and in the Google Chat card —
 * points at a page only signed-in people may see. Clicking it while signed out has to end on that
 * page once the sign-in is done, not on the dashboard: an alert whose link loses its way is an alert
 * that made somebody go looking for what it was already holding.
 */
test("a link clicked while signed out lands where it pointed once signed in", async ({ page }) => {
  // Somebody has to exist before anybody can sign in as them; this also puts us back outside.
  await signIn(page);
  await signOut(page);

  // No telemetry needed: what is under test is the address surviving the door, and an Episode that
  // is not there renders an empty panel rather than a failure.
  const episodeId = "0199c0de-1a2b-7c3d-8e4f-5a6b7c8d9e0f";
  await page.goto(`/episodes?episode=${episodeId}`);

  // Turned away — and the address came along.
  await expect(page).toHaveURL(/\/login\?redirect=/);
  await expect(page.getByText("Sign in to Bugler")).toBeVisible();

  await signInWithForm(page);

  await expect(page).toHaveURL(new RegExp(`/episodes\\?.*episode=${episodeId}`));
  await expect(page.getByRole("heading", { name: "Episodes" })).toBeVisible();
});

/**
 * The other half of the same door. A sign-in page that forwards wherever it is asked to is a
 * phishing tool wearing the operator's own domain, so an address leading off this server is not
 * honoured — the visitor arrives where they would have without a link at all.
 */
test("a sign-in link pointing off the server falls back to the dashboard", async ({ page }) => {
  await signIn(page);
  await signOut(page);

  await page.goto("/login?redirect=https://elsewhere.example/gotcha");
  await signInWithForm(page);

  await expect(page).toHaveURL(/\/dashboard$/);
});

async function signOut(page: Page) {
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByText("Sign in to Bugler")).toBeVisible();
}

async function signInWithForm(page: Page) {
  await page.getByLabel("E-mail").fill(ADMIN_EMAIL);
  await page.getByLabel("Password").fill(ADMIN_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
}
