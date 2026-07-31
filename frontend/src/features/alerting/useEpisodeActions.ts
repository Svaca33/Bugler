import { useMutation, useQueryClient } from "@tanstack/react-query";

import { api } from "@/api/client";

/**
 * The human hands on an Episode — acknowledge, take over, withdraw, solve — shared by the band
 * cards and the detail panel. Every alerts query keys under ["alerts"], so one invalidation
 * refreshes the list, the band, the counts, the nav badge, the detail and the history alike.
 */
export function useEpisodeActions(episodeId: string) {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["alerts"] });

  const acknowledge = useMutation({
    mutationFn: async () => {
      const { response } = await api.POST("/api/alerting/episodes/{id}/acknowledge", {
        params: { path: { id: episodeId } },
      });
      if (response.status === 409) throw new Error("Already solved — a Solved Episode is never acknowledged.");
      if (!response.ok) throw new Error("The acknowledgement was not saved.");
    },
    onSettled: refresh,
  });

  const withdraw = useMutation({
    mutationFn: async () => {
      const { response } = await api.DELETE("/api/alerting/episodes/{id}/acknowledgement", {
        params: { path: { id: episodeId } },
      });
      if (!response.ok) throw new Error("The acknowledgement was not withdrawn.");
    },
    onSettled: refresh,
  });

  const solve = useMutation({
    mutationFn: async () => {
      const { response } = await api.POST("/api/alerting/episodes/{id}/solve", {
        params: { path: { id: episodeId } },
      });
      if (response.status === 409) throw new Error("Already solved by someone else.");
      if (!response.ok) throw new Error("The verdict was not saved.");
    },
    onSettled: refresh,
  });

  const failure = acknowledge.error ?? withdraw.error ?? solve.error;
  return { acknowledge, withdraw, solve, failure };
}
