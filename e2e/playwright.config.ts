import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: "html",
  use: {
    baseURL: "http://localhost:3000",
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  // Requires a running PostgreSQL: `docker compose up -d postgres` first.
  webServer: [
    {
      command: "dotnet run --project ../src/Bugler.Host --no-launch-profile",
      url: "http://127.0.0.1:8080/health",
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
    {
      command: "bun dev",
      cwd: "../frontend",
      url: "http://localhost:3000",
      reuseExistingServer: !process.env.CI,
    },
  ],
});
