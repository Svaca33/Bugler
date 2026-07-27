import { useQuery } from "@tanstack/react-query";

import { api } from "./client";

/** The caller's visible applications and instances — feeds filters and admin pickers. */
export function useCatalog() {
  return useQuery({
    queryKey: ["catalog"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/catalog");
      if (error !== undefined) throw new Error("Failed to load catalog");
      return data;
    },
  });
}
