import { queryOptions, useQuery } from "@tanstack/react-query";

import { getMessages } from "@/i18n/runtime";
import { api } from "./client";

/**
 * Who is signed in — an app-wide question (layout, admin gate, who acts on an Episode). Spelled as
 * shared options rather than only a hook because the gate on `/_app` awaits the same question
 * before it lets anybody in: the router and the components then read one cache entry, not two.
 *
 * Null is an answer, not a failure: 401 is what "nobody" sounds like over HTTP.
 */
export const currentUserQuery = queryOptions({
  queryKey: ["auth", "me"],
  queryFn: async () => {
    const { data, response } = await api.GET("/api/auth/me");
    if (response.status === 401) return null;
    if (data === undefined) throw new Error(getMessages().common.loadFailed.session);
    return data;
  },
  retry: false,
  staleTime: 60_000,
});

export function useCurrentUser() {
  return useQuery(currentUserQuery);
}

/**
 * What the screens before sign-in need — and what the LanguageProvider reads the server's
 * language from, which is why it sits here beside "who is signed in": one cache entry serves
 * the login page and the language choice alike.
 */
export const authStatusQuery = queryOptions({
  queryKey: ["auth", "status"],
  queryFn: async () => {
    const { data, error } = await api.GET("/api/auth/status");
    if (error !== undefined) throw new Error(getMessages().common.loadFailed.session);
    return data;
  },
  retry: false,
  staleTime: 60_000,
});

/**
 * The Releases of the Services a Source Filter addresses over a window, plus what each was already
 * running as the window opened (ADR 0016).
 *
 * Narrowed by the Source Filter and the window and by nothing else — a deployment happened whoever's
 * Log Records the reader is looking at — so the key deliberately holds no severity, no full text and
 * no Attribute Filter. Two callers asking for the same Services over the same window share one
 * answer, which is why this lives here rather than inside either of them.
 */
export function useReleases(
  query: {
    applicationId?: string;
    namespace?: string;
    environment?: string;
    service?: string;
    range?: string;
    from?: string;
    to?: string;
  },
  options?: { enabled?: boolean; keepPrevious?: boolean },
) {
  return useQuery({
    queryKey: ["releases", query],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/releases", { params: { query } });
      if (error !== undefined) throw new Error("Failed to load releases");
      return data;
    },
    enabled: options?.enabled ?? true,
    // Holding the last answer while the next loads keeps markers from blinking out mid-refetch.
    placeholderData: options?.keepPrevious === true ? previous => previous : undefined,
  });
}

/**
 * The caller's applications and services — feeds Source Filters and admin pickers.
 *
 * By default it answers through the reader's Focus, which is what keeps an application they are
 * not watching out of every dropdown at once. `scope: "all"` asks for everything they may read
 * instead, and only settings screens may: the Focus card, which would otherwise be unable to offer
 * what is not already chosen, and the People tab, whose grant columns are somebody else's reading.
 */
export function useCatalog(options?: { refetchInterval?: number; scope?: "all" }) {
  const scope = options?.scope;
  return useQuery({
    queryKey: ["catalog", scope ?? "focus"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/catalog", {
        params: { query: scope === undefined ? {} : { scope } },
      });
      if (error !== undefined) throw new Error("Failed to load catalog");
      return data;
    },
    refetchInterval: options?.refetchInterval,
  });
}
