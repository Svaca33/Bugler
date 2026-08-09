import { createFileRoute } from "@tanstack/react-router";

import { SettingsPage } from "@/components/ui/settings-layout";
import { LanguageCard } from "@/features/access/LanguageCard";
import { PasswordCard } from "@/features/access/PasswordCard";
import { MachineDelegationsCard } from "@/features/mcp/MachineDelegationsCard";
import { useT } from "@/i18n";

export const Route = createFileRoute("/_app/account")({
  component: AccountRoute,
});

/**
 * Your own account, reached from your e-mail in the header. A page rather than a dialog because
 * there is now more than a form behind that door — and the page is where things from different
 * contexts are allowed to sit side by side, which is why the delegations card can be here at all
 * while the features themselves stay apart.
 */
function AccountRoute() {
  const t = useT();
  return (
    <SettingsPage title={t.access.account.title} description={t.access.account.description}>
      <LanguageCard />
      <PasswordCard />
      <MachineDelegationsCard />
    </SettingsPage>
  );
}
