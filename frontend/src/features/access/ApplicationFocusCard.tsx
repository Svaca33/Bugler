import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { useCatalog, useCurrentUser } from "@/api/queries";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { SettingsCard } from "@/components/ui/settings-layout";
import { useT } from "@/i18n";

/**
 * Which applications you are watching. A Focus is otherwise silent — no banner, no count over the
 * app — so this card carries the whole of its explanation, and says plainly when it holds nothing:
 * that is the one state where a silent Focus would read as a broken Bugler.
 *
 * The list is the whole of what you may read (`scope: "all"`), not what you are already watching,
 * because a card that offered only the ticked ones could never be widened again.
 */
export function ApplicationFocusCard() {
  const t = useT();
  const user = useCurrentUser();
  const catalog = useCatalog({ scope: "all" });
  const attend = useAttendToApplication();
  const [failed, setFailed] = useState(false);

  const applications = catalog.data?.applications ?? [];
  const focused = new Set(user.data?.focusedApplicationIds ?? []);

  return (
    <SettingsCard caption={t.access.focus.caption}>
      <p className="text-[12.5px] text-[#8CA1B8]">{t.access.focus.description}</p>

      {applications.length === 0 ? (
        <p className="text-sm text-[#8CA1B8]">{t.access.focus.nothingToAttendTo}</p>
      ) : (
        <div className="grid gap-2.5">
          {applications.map(application => (
            <div key={application.id} className="flex items-center gap-2.5">
              <Checkbox
                id={`focus-${application.id}`}
                checked={focused.has(application.id)}
                onCheckedChange={checked => {
                  setFailed(false);
                  attend.mutate(
                    { applicationId: application.id, watching: checked === true },
                    { onError: () => setFailed(true) },
                  );
                }}
              />
              <Label htmlFor={`focus-${application.id}`} className="font-normal">
                {application.name}
              </Label>
            </div>
          ))}
        </div>
      )}

      {/*
        Said here rather than over the app: this is where the choice is made, so this is where its
        consequence belongs. Only while the catalog has actually answered — "no applications yet"
        is a different emptiness and has its own sentence above.
      */}
      {applications.length > 0 && focused.size === 0 && (
        <p className="text-sm text-[#F6C170]">{t.access.focus.attendingToNothing}</p>
      )}

      {failed && <p className="text-sm text-destructive">{t.access.focus.saveFailed}</p>}
    </SettingsCard>
  );
}

/**
 * One row per click, like the grant checkboxes on the People tab. The catalog goes with the
 * session because everything it feeds — every filter, every list — has just changed shape.
 */
function useAttendToApplication() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: { applicationId: string; watching: boolean }) => {
      const request = { params: { path: { applicationId: input.applicationId } } };
      const { response } = input.watching
        ? await api.PUT("/api/auth/focus/{applicationId}", request)
        : await api.DELETE("/api/auth/focus/{applicationId}", request);
      if (!response.ok) throw new Error("Failed to save the focus");
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["auth", "me"] });
      void queryClient.invalidateQueries({ queryKey: ["catalog"] });
    },
  });
}
