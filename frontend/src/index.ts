import { serve } from "bun";
import index from "./index.html";

const API_ORIGIN = process.env.BUGLER_API ?? "http://127.0.0.1:8080";

/** Forwards API traffic to the Bugler backend so cookies stay first-party in dev. */
function proxy(req: Request): Promise<Response> {
  const url = new URL(req.url);
  return fetch(new Request(API_ORIGIN + url.pathname + url.search, req));
}

const server = serve({
  routes: {
    "/api/*": proxy,
    "/openapi/*": proxy,

    // Serve the SPA for all other routes.
    "/*": index,
  },

  development: process.env.NODE_ENV !== "production" && {
    hmr: true,
    console: true,
  },
});

console.log(`🚀 Bugler frontend running at ${server.url}`);
