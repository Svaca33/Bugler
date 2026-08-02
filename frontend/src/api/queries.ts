import { useQuery } from "@tanstack/react-query";

import { api } from "./client";

/** Who is signed in — an app-wide question (layout, admin gate, who acts on an Episode). */
export function useCurrentUser() {
  return useQuery({
    queryKey: ["auth", "me"],
    queryFn: async () => {
      const { data, response } = await api.GET("/api/auth/me");
      if (response.status === 401) return null;
      if (data === undefined) throw new Error("Failed to load session");
      return data;
    },
    retry: false,
    staleTime: 60_000,
  });
}

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

/** The caller's visible applications and services — feeds Source Filters and admin pickers. */
export function useCatalog(options?: { refetchInterval?: number }) {
  return useQuery({
    queryKey: ["catalog"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/catalog");
      if (error !== undefined) throw new Error("Failed to load catalog");
      return data;
    },
    refetchInterval: options?.refetchInterval,
  });
}
