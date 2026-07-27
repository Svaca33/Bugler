---
status: accepted
---

# Bun as the complete frontend toolchain

The frontend is built, served in development, tested, and package-managed by **Bun** alone — no Node, no Vite. Bun's bundler covers the SPA workflow natively (HTML entrypoint, dev server with HMR/Fast Refresh, static production build, Tailwind via `bun-plugin-tailwind`), and one tool replacing three keeps the toolchain simple and fast.

## Considered Options

- **Vite on Node** — the mainstream choice with the larger plugin ecosystem, including the official TanStack Router bundler plugin. Rejected in favor of a single toolchain.

## Consequences

TanStack Router's file-based route generation runs through `@tanstack/router-cli` (`tsr watch`) instead of a bundler plugin. CI and developer machines need Bun installed, not Node. Supersedes the Vite mention in ADR 0004.
