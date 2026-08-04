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
import { LanguageProvider, getMessages } from "./i18n";

import "./index.css";

// Explore's queries read production-sized windows, so a failed one is usually a query the server
// could not finish rather than a packet that went missing — and the library's default of three
// retries turns that into four runs of the most expensive thing on the page, at the moment the
// server is least able to afford it. Refetching on window focus does the same on every tab switch.
// Both lists carry an explicit Refresh, and their error state offers a retry, so renewing stays
// something asked for.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});

const router = createRouter({
  routeTree,
  context: { queryClient },
  // The gate on `/_app` awaits "who is signed in" before the shell renders, so there is a moment
  // with nothing on screen. It is one call long and usually invisible; this is what shows if the
  // server is slow enough for the wait to be noticed.
  defaultPendingComponent: () => (
    <div className="grid min-h-screen place-items-center text-muted-foreground">
      {getMessages().common.loading}
    </div>
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
      {/* Above the router: the language is chosen once, and every screen — the sign-in page
          included — speaks it. */}
      <LanguageProvider>
        <RouterProvider router={router} />
      </LanguageProvider>
    </QueryClientProvider>
  </StrictMode>
);

// https://bun.com/docs/bundler/hot-reloading#import-meta-hot-data
(import.meta.hot.data.root ??= createRoot(elem)).render(app);
