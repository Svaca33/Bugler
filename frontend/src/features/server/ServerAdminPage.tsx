import { useT } from "@/i18n";

import { MailSettingsCard } from "./MailSettingsCard";
import { ServerLanguageCard } from "./ServerLanguageCard";

/** Deployment diagnostics: things that are true of this server rather than of any application. */
export function ServerAdminPage() {
  const t = useT();
  return (
    <div className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex flex-col gap-1">
        <h2 className="text-[17px] font-semibold tracking-[-0.3px]">{t.server.page.title}</h2>
        <p className="text-[12.5px] text-[#8CA1B8]">{t.server.page.subtitle}</p>
      </div>

      <ServerLanguageCard />
      <MailSettingsCard />
    </div>
  );
}
