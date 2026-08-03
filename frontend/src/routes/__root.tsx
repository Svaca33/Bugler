import type { QueryClient } from "@tanstack/react-query";
import { Outlet, createRootRouteWithContext } from "@tanstack/react-router";

/**
 * What every route may count on having. The query cache is in here because the gate on `/_app` has
 * to ask who is signed in before it renders anything, and it must ask it of the same cache the
 * pages read — otherwise the answer would be fetched twice and could disagree with itself.
 */
export interface RouterContext {
  queryClient: QueryClient;
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: () => <Outlet />,
});
