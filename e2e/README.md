# Bugler E2E tests

Playwright tests exercising the whole stack through the browser.

```bash
bunx playwright install chromium   # once per machine
bun run test                       # starts the frontend dev server automatically
```

Needs `docker compose up -d postgres mailpit`. The password-reset spec reads the mail Bugler
actually sent out of mailpit's API on :8025, so without it that spec fails while the rest pass.
