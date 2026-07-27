import { test, expect } from "@playwright/test";

test("frontend serves the app shell", async ({ page }) => {
  await page.goto("/");

  await expect(page.locator("#root")).toBeAttached();
});
