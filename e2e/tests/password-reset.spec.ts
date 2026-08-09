import { expect, test, type APIRequestContext } from "@playwright/test";

import { signIn } from "./helpers";

/**
 * The Password Reset all the way through the mail: ask for a link on the sign-in form, read the
 * mail Bugler actually sent out of mailpit, follow it, set a password and sign in with it.
 *
 * Needs the mailpit from docker-compose (`docker compose up -d mailpit`).
 */
const MAILPIT = "http://localhost:8025";

test("a forgotten password is reset through the link that arrives by mail", async ({ page, request }) => {
  test.setTimeout(120_000);

  // An account of its own: resetting the shared admin would lock every other spec out.
  const email = `reset-e2e-${Date.now()}@bugler.test`;
  const firstPassword = "FirstPass123!";
  const newPassword = "SecondPass456!";

  await signIn(page);
  await page.getByRole("link", { name: "Admin", exact: true }).click();
  await page.getByRole("button", { name: "People" }).click();
  await page.getByLabel("E-mail", { exact: true }).fill(email);
  await page.getByLabel("Password (min 8 characters)").fill(firstPassword);
  await page.getByRole("button", { name: "Create user" }).click();
  await expect(page.getByTitle(email)).toBeVisible();
  await page.getByRole("button", { name: "Sign out" }).click();

  // Forget the password.
  await expect(page.getByText("Sign in to Bugler")).toBeVisible();
  await page.getByRole("link", { name: "Forgot your password?" }).click();
  await page.getByLabel("E-mail").fill(email);
  await page.getByRole("button", { name: "Send the link" }).click();
  await expect(page.getByText("Check your mail")).toBeVisible();

  const token = await waitForResetToken(request, email);

  // The link in the mail names the server's own public origin, which in a dev run is not where
  // this browser is looking. The secret is what matters, so it is carried over by hand.
  await page.goto(`/reset-password?token=${encodeURIComponent(token)}`);
  await expect(page.getByText("Set a new password")).toBeVisible();
  await page.getByLabel("New password (min 8 characters)").fill(newPassword);
  await page.getByLabel("Repeat new password").fill(newPassword);
  await page.getByRole("button", { name: "Set password" }).click();

  // No automatic sign-in: the reset hands you back to the sign-in form.
  await expect(page.getByText("Sign in to Bugler")).toBeVisible();
  await page.getByLabel("E-mail").fill(email);
  await page.getByLabel("Password").fill(newPassword);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("link", { name: "Logs" })).toBeVisible();
});

/** Polls mailpit until Bugler's background loop has actually delivered the message. */
async function waitForResetToken(request: APIRequestContext, email: string) {
  let body = "";

  await expect
    .poll(
      async () => {
        // "query", not "q" — mailpit answers the latter with "no search query".
        const found = await request.get(
          `${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:${email}`)}`,
        );
        if (!found.ok()) return 0;

        const { messages } = (await found.json()) as { messages: { ID?: string; id?: string }[] };
        if (messages.length === 0) return 0;

        const message = await request.get(`${MAILPIT}/api/v1/message/${messages[0].ID ?? messages[0].id}`);
        const json = (await message.json()) as { Text?: string; text?: string };
        body = json.Text ?? json.text ?? "";
        return body.length > 0 ? messages.length : 0;
      },
      { timeout: 30_000, intervals: [500], message: `No reset mail reached mailpit for ${email}` },
    )
    .toBeGreaterThan(0);

  const link = /\?token=([^\s]+)/.exec(body);
  expect(link, `No reset link in the mail:\n${body}`).not.toBeNull();
  return decodeURIComponent(link![1]);
}
