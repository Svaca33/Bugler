import { useMutation, useQueryClient } from "@tanstack/react-query";

import { api } from "@/api/client";
import { getMessages } from "@/i18n/runtime";

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
      const { error, response } = await api.POST("/api/alerting/episodes/{id}/acknowledge", {
        params: { path: { id: episodeId } },
      });
      const words = getMessages().alerting.actions;
      if (response.status === 409) {
        throw new Error(refusal(error, words.alreadySolvedNoAck));
      }
      if (!response.ok) throw new Error(words.ackNotSaved);
    },
    onSettled: refresh,
  });

  const withdraw = useMutation({
    mutationFn: async () => {
      const { error, response } = await api.DELETE("/api/alerting/episodes/{id}/acknowledgement", {
        params: { path: { id: episodeId } },
      });
      const words = getMessages().alerting.actions;
      if (response.status === 409) {
        throw new Error(refusal(error, words.withdrawFailed));
      }
      if (!response.ok) throw new Error(words.withdrawFailed);
    },
    onSettled: refresh,
  });

  const solve = useMutation({
    mutationFn: async () => {
      const { error, response } = await api.POST("/api/alerting/episodes/{id}/solve", {
        params: { path: { id: episodeId } },
      });
      const words = getMessages().alerting.actions;
      if (response.status === 409) throw new Error(refusal(error, words.alreadySolvedByOther));
      if (!response.ok) throw new Error(words.verdictNotSaved);
    },
    onSettled: refresh,
  });

  // The human answers to the machine hand (Alerting CONTEXT.md): reject its proposal, sweep
  // its resignation aside, withdraw its claim. Confirming a proposal is `solve` above.
  const rejectProposal = useMutation({
    mutationFn: async () => {
      const { error, response } = await api.POST("/api/alerting/episodes/{id}/proposal/reject", {
        params: { path: { id: episodeId } },
      });
      const words = getMessages().alerting.machine;
      if (response.status === 409) throw new Error(refusal(error, words.rejectFailed));
      if (!response.ok) throw new Error(words.rejectFailed);
    },
    onSettled: refresh,
  });

  const dismissResignation = useMutation({
    mutationFn: async () => {
      const { error, response } = await api.DELETE("/api/alerting/episodes/{id}/resignation", {
        params: { path: { id: episodeId } },
      });
      const words = getMessages().alerting.machine;
      if (response.status === 409) throw new Error(refusal(error, words.dismissFailed));
      if (!response.ok) throw new Error(words.dismissFailed);
    },
    onSettled: refresh,
  });

  const withdrawClaim = useMutation({
    mutationFn: async () => {
      const { error, response } = await api.DELETE("/api/alerting/episodes/{id}/claim", {
        params: { path: { id: episodeId } },
      });
      const words = getMessages().alerting.machine;
      if (response.status === 409) throw new Error(refusal(error, words.withdrawClaimFailed));
      if (!response.ok) throw new Error(words.withdrawClaimFailed);
    },
    onSettled: refresh,
  });

  const failure =
    acknowledge.error
    ?? withdraw.error
    ?? solve.error
    ?? rejectProposal.error
    ?? dismissResignation.error
    ?? withdrawClaim.error;
  return { acknowledge, withdraw, solve, rejectProposal, dismissResignation, withdrawClaim, failure };
}

/** A 409 carries the model's own sentence (e.g. "The action belongs to the newest Episode of its
 * kind.") — shown verbatim in whatever language the server spoke; the fallback is the catalog's. */
function refusal(error: unknown, fallback: string): string {
  return typeof error === "string" && error.length > 0 ? error : fallback;
}
