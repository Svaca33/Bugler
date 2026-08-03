import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { authStatusQuery } from "@/api/queries";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { LANGUAGES, LANGUAGE_AUTONYMS, toLanguage, useT } from "@/i18n";
import { getMessages } from "@/i18n/runtime";

const CAPTION = "font-mono text-[11px] tracking-[0.08em] text-[#7D93AA]";

const LANGUAGE_KEY = ["admin", "server", "language"] as const;

/**
 * The server's language: what this deployment speaks to the sign-in screen, to a chat room, and
 * to every account that follows the server rather than choosing (ADR 0024). Saving is the change
 * itself — one select, no form.
 */
export function ServerLanguageCard() {
  const t = useT();
  const queryClient = useQueryClient();
  const [failed, setFailed] = useState(false);

  const language = useQuery({
    queryKey: LANGUAGE_KEY,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/server/language", {});
      if (error !== undefined || data === undefined) {
        throw new Error(getMessages().server.language.loadFailed);
      }
      return data;
    },
  });

  const save = useMutation({
    mutationFn: async (code: string) => {
      const { data, response } = await api.PUT("/api/admin/server/language", {
        body: { language: code },
      });
      if (!response.ok || data === undefined) {
        throw new Error(getMessages().server.language.saveFailed);
      }
      return data;
    },
    onSuccess: saved => {
      queryClient.setQueryData(LANGUAGE_KEY, saved);
      // The status query carries the server's language to the LanguageProvider — refetching it
      // is what re-speaks the UI for everyone who follows the server.
      void queryClient.invalidateQueries({ queryKey: authStatusQuery.queryKey });
    },
    onError: () => setFailed(true),
  });

  return (
    <div className="flex max-w-[620px] flex-col gap-4 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <span className={CAPTION}>{t.server.language.caption}</span>
      <p className="text-[12.5px] leading-relaxed text-[#8CA1B8]">{t.server.language.intro}</p>

      {language.isError && (
        <p className="text-[12.5px] text-[#F0685A]">{t.server.language.loadFailed}</p>
      )}

      {language.data !== undefined && (
        <div className="grid max-w-[280px] gap-2">
          <Label htmlFor="server-language">{t.server.language.label}</Label>
          <Select
            value={toLanguage(language.data.language) ?? "en"}
            onValueChange={value => {
              setFailed(false);
              save.mutate(value);
            }}
          >
            <SelectTrigger id="server-language" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {LANGUAGES.map(code => (
                <SelectItem key={code} value={code}>
                  {LANGUAGE_AUTONYMS[code]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {failed && (
            <p className="text-[12.5px] text-[#F0685A]">{t.server.language.saveFailed}</p>
          )}
        </div>
      )}
    </div>
  );
}
