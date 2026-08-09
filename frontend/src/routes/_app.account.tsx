import { createFileRoute } from "@tanstack/react-router";

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
    <div className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex max-w-[620px] flex-col gap-1">
        <h1 className="text-[19px] font-semibold tracking-[-0.4px]">{t.access.account.title}</h1>
        <p className="text-[12.5px] text-[#8CA1B8]">{t.access.account.description}</p>
      </div>

      <LanguageCard />
      <PasswordCard />
      <MachineDelegationsCard />
    </div>
  );
}
