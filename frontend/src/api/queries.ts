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
