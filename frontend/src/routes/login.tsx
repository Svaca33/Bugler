import { createFileRoute, redirect } from "@tanstack/react-router";

import { currentUserQuery } from "@/api/queries";
import { LoginPage } from "@/features/access/LoginPage";
import { DEFAULT_DESTINATION, sanitizeRedirectTarget } from "@/features/access/redirectTarget";

/** Where the visitor was heading before the gate sent them here; absent when they came on their own. */
export interface LoginSearch {
  redirect?: string;
}

export const Route = createFileRoute("/login")({
  // The single door the `redirect` value comes through. Sanitizing it here rather than at the point
  // of use means nothing downstream can be the thing that forgot: what the page reads is already
  // an address inside this app, or nothing at all.
  validateSearch: (search: Record<string, unknown>): LoginSearch => ({
    redirect: sanitizeRedirectTarget(search.redirect),
  }),
  // Somebody already signed in has no business looking at a sign-in form — send them on to wherever
  // they were going. This is also what keeps Back honest after a sign-in.
  beforeLoad: async ({ context, search }) => {
    const user = await context.queryClient.ensureQueryData(currentUserQuery);
    if (user != null) {
      // `href` rather than `to`: what we carry is a whole address, search params and all, and `to`
      // names a route path — it would take "/episodes?episode=…" for a path spelled just like that.
      throw redirect({ href: search.redirect ?? DEFAULT_DESTINATION, replace: true });
    }
  },
  component: LoginRoute,
});

function LoginRoute() {
  const { redirect: destination } = Route.useSearch();
  return <LoginPage destination={destination ?? DEFAULT_DESTINATION} />;
}
