/**
 * Entry point for the React app: creates the TanStack router and
 * mounts it together with the TanStack Query client.
 *
 * It is included in `src/index.html`.
 */

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider, createRouter } from "@tanstack/react-router";

import { routeTree } from "./routeTree.gen";

import "./index.css";

const queryClient = new QueryClient();

const router = createRouter({
  routeTree,
  context: { queryClient },
  // The gate on `/_app` awaits "who is signed in" before the shell renders, so there is a moment
  // with nothing on screen. It is one call long and usually invisible; this is what shows if the
  // server is slow enough for the wait to be noticed.
  defaultPendingComponent: () => (
    <div className="grid min-h-screen place-items-center text-muted-foreground">Loading…</div>
  ),
});

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}

const elem = document.getElementById("root")!;
const app = (
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>
);

// https://bun.com/docs/bundler/hot-reloading#import-meta-hot-data
(import.meta.hot.data.root ??= createRoot(elem)).render(app);
