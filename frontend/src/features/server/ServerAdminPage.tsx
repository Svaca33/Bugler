import type { ReactNode } from "react";

import { SettingsPage } from "@/components/ui/settings-layout";
import { useT } from "@/i18n";

import { AiSettingsCard } from "./AiSettingsCard";
import { MailSettingsCard } from "./MailSettingsCard";
import { ServerLanguageCard } from "./ServerLanguageCard";

/**
 * Deployment diagnostics: things that are true of this server rather than of any application.
 *
 * Cards from other contexts arrive as children rather than as imports — a page may combine what the
 * features may not reach across, and the route is where that is decided.
 */
export function ServerAdminPage(props: { children?: ReactNode }) {
  const t = useT();
  return (
    <SettingsPage
      title={t.server.page.title}
      description={t.server.page.subtitle}
      headingLevel={2}
    >
      <ServerLanguageCard />
      <MailSettingsCard />
      <AiSettingsCard />
      {props.children}
    </SettingsPage>
  );
}
