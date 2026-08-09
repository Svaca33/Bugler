import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import type { components } from "@/api/schema";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useT } from "@/i18n";

import { MCP_CONNECTION_KEY } from "./useMachineDelegations";

type McpSettings = components["schemas"]["McpSettingsDto"];

const CAPTION = "font-mono text-[11px] tracking-[0.08em] text-[#7D93AA]";

const SETTINGS_KEY = ["admin", "mcp", "settings"] as const;

/**
 * Whether this server opens a machine door at all, and the address it answers at (ADR 0030) — the
 * mail and AI cards' sibling: stored here from the first save, reset returns it to the deployment's
 * configuration. What each delegation may then read is still its holder's own Visibility Scope.
 */
export function McpSettingsCard() {
  const t = useT();
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState<McpSettings | null>(null);

  const settings = useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/mcp/settings", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to load the MCP settings.");
      }
      return data;
    },
    // While an admin is typing here, a focus refetch must not clobber the form.
    refetchOnWindowFocus: false,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: SETTINGS_KEY });
    // The page that shows people where to point their tool reads the same fact.
    void queryClient.invalidateQueries({ queryKey: MCP_CONNECTION_KEY });
  };

  const save = useMutation({
    mutationFn: async (body: { opened: boolean; publicUrl: string }) => {
      const { data, error } = await api.PUT("/api/admin/mcp/settings", { body });
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to save the MCP settings.");
      }
      return data;
    },
    onSuccess: settings => {
      setDraft(settings);
      invalidate();
    },
  });

  const reset = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.DELETE("/api/admin/mcp/settings", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to reset the MCP settings.");
      }
      return data;
    },
    onSuccess: settings => {
      setDraft(settings);
      invalidate();
    },
  });

  const current = draft ?? settings.data;

  return (
    <div className="flex max-w-[620px] flex-col gap-4 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <div className="flex items-center gap-3">
        <span className={CAPTION}>{t.mcp.settings.title}</span>
        {current !== undefined && (
          <span className="ml-auto text-[11px] text-[#7D93AA]">
            {current.source === "Stored"
              ? t.mcp.settings.fromStored
              : t.mcp.settings.fromConfiguration}
          </span>
        )}
      </div>

      <p className="text-[12.5px] text-[#8CA1B8]">{t.mcp.settings.description}</p>

      {current !== undefined && (
        <form
          className="grid gap-3.5"
          onSubmit={event => {
            event.preventDefault();
            save.mutate({ opened: current.opened, publicUrl: current.publicUrl });
          }}
        >
          <label className="flex items-start gap-2.5">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={current.opened}
              onChange={event => setDraft({ ...current, opened: event.target.checked })}
            />
            <span className="flex flex-col gap-0.5">
              <span className="text-[13px] text-[#DCE8F3]">{t.mcp.settings.openedLabel}</span>
              <span className="text-[11.5px] text-[#7D93AA]">{t.mcp.settings.openedHint}</span>
            </span>
          </label>

          <div className="grid gap-1.5">
            <Label htmlFor="mcp-address">{t.mcp.settings.addressLabel}</Label>
            <Input
              id="mcp-address"
              value={current.publicUrl}
              placeholder={t.mcp.settings.addressPlaceholder}
              onChange={event => setDraft({ ...current, publicUrl: event.target.value })}
            />
            <span className="text-[11.5px] text-[#7D93AA]">{t.mcp.settings.addressHint}</span>
          </div>

          {save.isError && <p className="text-[12.5px] text-[#F0685A]">{save.error.message}</p>}

          <div className="flex items-center gap-2">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? t.mcp.settings.saving : t.mcp.settings.save}
            </Button>
            {current.source === "Stored" && (
              <Button
                type="button"
                variant="outline"
                disabled={reset.isPending}
                onClick={() => reset.mutate()}
              >
                {t.mcp.settings.reset}
              </Button>
            )}
            {save.isSuccess && (
              <span className="text-[12px] text-[#6FBF8B]">{t.mcp.settings.saved}</span>
            )}
          </div>
        </form>
      )}
    </div>
  );
}
