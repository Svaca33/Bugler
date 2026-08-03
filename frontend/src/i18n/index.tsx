import { useQuery } from "@tanstack/react-query";
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

import { authStatusQuery, currentUserQuery } from "@/api/queries";
import { en } from "./en";
import { toLanguage, type LanguageCode } from "./language";
import type { Messages } from "./messages";
import { activate } from "./runtime";

export { FORMAT_LOCALES, LANGUAGES, LANGUAGE_AUTONYMS, toLanguage } from "./language";
export type { LanguageCode } from "./language";
export type { Messages } from "./messages";
export { plural } from "./plural";
export { getActiveLanguage, getFormatLocale, getMessages } from "./runtime";

const catalogs = new Map<LanguageCode, Messages>([["en", en]]);

// Dynamic imports so only the language a person reads rides in their bundle; English is the
// statically bundled last resort. One arm per supported language — the compiler minds the rest.
const loaders: Record<LanguageCode, () => Promise<Messages>> = {
  en: () => Promise.resolve(en),
  cs: () => import("./cs").then(module => module.cs),
};

interface ActiveLanguage {
  code: LanguageCode;
  messages: Messages;
}

const LanguageContext = createContext<ActiveLanguage>({ code: "en", messages: en });

/**
 * Chooses what language the UI speaks: the signed-in person's own choice, the server's language
 * for everyone who never chose (and for the screens before sign-in), English while nothing has
 * answered yet. Renders English for at most the moment a catalog chunk takes to arrive — the
 * router's auth gate is waiting on the same queries anyway.
 */
export function LanguageProvider({ children }: { children: ReactNode }) {
  const user = useQuery(currentUserQuery);
  const status = useQuery(authStatusQuery);
  const target: LanguageCode =
    toLanguage(user.data?.language) ?? toLanguage(status.data?.language) ?? "en";

  const [active, setActive] = useState<ActiveLanguage>({ code: "en", messages: en });

  useEffect(() => {
    let cancelled = false;
    const ready = catalogs.get(target);
    if (ready !== undefined) {
      apply({ code: target, messages: ready });
      return;
    }

    loaders[target]().then(messages => {
      catalogs.set(target, messages);
      if (!cancelled) apply({ code: target, messages });
    });
    return () => {
      cancelled = true;
    };

    function apply(next: ActiveLanguage) {
      activate(next.code, next.messages);
      document.documentElement.lang = next.code;
      setActive(current => (current.code === next.code ? current : next));
    }
  }, [target]);

  return <LanguageContext.Provider value={active}>{children}</LanguageContext.Provider>;
}

/** Everything the UI says, in the active language: `const t = useT();` then `t.nav.logs`. */
export function useT(): Messages {
  return useContext(LanguageContext).messages;
}

export function useLanguage(): LanguageCode {
  return useContext(LanguageContext).code;
}
