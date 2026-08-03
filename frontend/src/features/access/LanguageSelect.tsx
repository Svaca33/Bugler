import { useMutation, useQueryClient } from "@tanstack/react-query";
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
import { useAuthStatus, useCurrentUser } from "@/features/access/useAuth";
import { LANGUAGES, LANGUAGE_AUTONYMS, toLanguage, useT } from "@/i18n";
import { getMessages } from "@/i18n/runtime";

/** What the inherit option is called in the wire format the Select needs: never a real code. */
const DEFAULT_OPTION = "default";

/**
 * The language picker behind the e-mail in the header. Saving is the change itself — no submit:
 * the choice lands on the account, the queries refetch, and the LanguageProvider re-speaks the
 * whole UI. "Server default" is a real option rather than an absence, because following the
 * server is a choice a person can return to (ADR 0024).
 */
export function LanguageSelect() {
  const t = useT();
  const user = useCurrentUser();
  const status = useAuthStatus();
  const setLanguage = useSetUserLanguage();
  const [failed, setFailed] = useState(false);

  const own = toLanguage(user.data?.language);
  const serverLanguage = toLanguage(status.data?.language) ?? "en";

  return (
    <div className="grid gap-2">
      <Label htmlFor="account-language">{t.nav.account.language}</Label>
      <Select
        value={own ?? DEFAULT_OPTION}
        onValueChange={value => {
          setFailed(false);
          setLanguage.mutate(value === DEFAULT_OPTION ? null : value, {
            onError: () => setFailed(true),
          });
        }}
      >
        <SelectTrigger id="account-language" className="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={DEFAULT_OPTION}>
            {t.nav.account.languageDefault(LANGUAGE_AUTONYMS[serverLanguage])}
          </SelectItem>
          {LANGUAGES.map(code => (
            <SelectItem key={code} value={code}>
              {LANGUAGE_AUTONYMS[code]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {failed && <p className="text-sm text-destructive">{t.nav.account.languageSaveFailed}</p>}
    </div>
  );
}

/** Persists the choice; the refetched session is what actually switches the words. */
function useSetUserLanguage() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (language: string | null) => {
      const { response } = await api.POST("/api/auth/language", { body: { language } });
      if (!response.ok) throw new Error(getMessages().nav.account.languageSaveFailed);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["auth", "me"] });
      void queryClient.invalidateQueries({ queryKey: authStatusQuery.queryKey });
    },
  });
}
