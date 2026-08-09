import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/api/client";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { useT } from "@/i18n";

const CAPTION = "font-mono text-[11px] tracking-[0.08em] text-[#7D93AA]";

/**
 * The application's AI Consent (ADR 0028): whether its telemetry may be shown to the server's
 * AI provider. The switch and the plain words of what leaves stand together on purpose — the
 * consent is only as informed as this sentence. Which provider that is lives on the Server tab.
 */
export function ApplicationAiCard(props: { applicationId: string }) {
  const t = useT();
  const queryClient = useQueryClient();

  // The applications list already carries the flag; this card reads its own row from it.
  const applications = useQuery({
    queryKey: ["admin", "applications"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/applications", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to load applications");
      }
      return data;
    },
  });
  const application = applications.data?.find(a => a.id === props.applicationId);

  const setConsent = useMutation({
    mutationFn: async (aiConsent: boolean) => {
      const { error } = await api.PUT("/api/admin/applications/{id}/ai-consent", {
        params: { path: { id: props.applicationId } },
        body: { aiConsent },
      });
      if (error !== undefined) {
        throw new Error(t.registry.aiCard.saveFailed);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "applications"] }),
  });

  const serverAi = useQuery({
    queryKey: ["admin", "ai", "settings"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/ai/settings", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to load the AI settings.");
      }
      return data;
    },
  });

  if (application === undefined) {
    return null;
  }

  return (
    <div className="flex max-w-[820px] flex-col gap-3 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <span className={CAPTION}>{t.registry.aiCard.caption}</span>

      <div className="flex items-start gap-2.5">
        <Checkbox
          id="ai-consent"
          className="mt-[2px]"
          checked={application.aiConsent}
          disabled={setConsent.isPending}
          onCheckedChange={checked => setConsent.mutate(checked === true)}
        />
        <div className="flex flex-col gap-1">
          <Label htmlFor="ai-consent">{t.registry.aiCard.consentLabel}</Label>
          <p className="max-w-[74ch] text-[12px] leading-relaxed text-[#8CA1B8]">
            {t.registry.aiCard.whatLeaves}
          </p>
          {serverAi.data !== undefined && !serverAi.data.isConfigured && (
            <p className="text-[12px] text-[#7D93AA]">{t.registry.aiCard.serverAiOffNote}</p>
          )}
        </div>
      </div>

      {setConsent.isError && (
        <p className="text-[12.5px] text-[#F0685A]">{t.registry.aiCard.saveFailed}</p>
      )}
    </div>
  );
}
