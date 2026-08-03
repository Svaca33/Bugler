import { useQuery } from "@tanstack/react-query";

import { api } from "@/api/client";
import { getMessages } from "@/i18n/runtime";

/**
 * The whole-scope open count behind the nav badge — deliberately unfiltered: the badge answers
 * "is anything burning that I may see", not "what does my current triage view show".
 */
export function useOpenEpisodeCount(enabled: boolean): number | undefined {
  const counts = useQuery({
    queryKey: ["alerts", "counts", "everything"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes/counts");
      if (error !== undefined) throw new Error(getMessages().alerting.errors.countOpenEpisodes);
      return data;
    },
    refetchInterval: 30_000,
    // The shell renders signed-out too; an unauthenticated badge poll would only collect 401s.
    enabled,
  });
  return counts.data === undefined ? undefined : Number(counts.data.open);
}
