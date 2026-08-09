import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { SettingsCard } from "@/components/ui/settings-layout";
import { getFormatLocale, useT } from "@/i18n";

import { useHeldMachineDelegations, useRevokeHeldMachineDelegation } from "./useMachineDelegations";

/**
 * Every Machine Delegation live on this server, beside the switch that let them exist. Whoever holds that
 * switch has to be able to see what it opened, or closing it is a decision made blind (ADR 0029) —
 * and revoking one is the narrower answer to a lost laptop than deactivating the person.
 */
export function HeldMachineDelegationsCard() {
  const t = useT();
  const locale = getFormatLocale();
  const held = useHeldMachineDelegations();
  const catalog = useCatalog();
  const revoke = useRevokeHeldMachineDelegation();

  const date = (value: string | null | undefined) =>
    value === null || value === undefined
      ? null
      : new Date(value).toLocaleString(locale, { dateStyle: "medium", timeStyle: "short" });

  const applicationName = (id: string | null | undefined) =>
    id === null || id === undefined
      ? t.mcp.list.scopeEverything
      : (catalog.data?.applications.find(a => a.id === id)?.name ?? id);

  return (
    <SettingsCard caption={t.mcp.held.title} className="gap-3">
      <p className="text-[12.5px] text-[#8CA1B8]">{t.mcp.held.description}</p>

      {held.data?.length === 0 && (
        <p className="text-[12.5px] text-[#8CA1B8]">{t.mcp.held.empty}</p>
      )}

      {(held.data ?? []).map(delegation => (
        <div
          key={delegation.id}
          className="flex items-center gap-4 border-t border-[#17293D] pt-3"
        >
          <div className="flex min-w-0 flex-1 flex-col gap-0.5">
            <span className="truncate text-[13px] text-[#DCE8F3]">{delegation.name}</span>
            <span className="truncate font-mono text-[11.5px] text-[#8CA1B8]">
              {delegation.userEmail}
            </span>
          </div>

          <dl className="hidden gap-5 text-[11.5px] sm:flex">
            <div className="flex flex-col gap-0.5">
              <dt className="text-[#7D93AA]">{t.mcp.list.scopeColumn}</dt>
              <dd className="text-[#A9BDD1]">{applicationName(delegation.applicationId)}</dd>
            </div>
            <div className="flex flex-col gap-0.5">
              <dt className="text-[#7D93AA]">{t.mcp.list.lastUsedColumn}</dt>
              <dd className="font-mono text-[#A9BDD1]">
                {date(delegation.lastUsedAt) ?? t.mcp.list.neverUsed}
              </dd>
            </div>
          </dl>

          <Button
            variant="outline"
            size="sm"
            disabled={revoke.isPending}
            onClick={() => {
              if (window.confirm(t.mcp.held.revokeConfirm(delegation.name, delegation.userEmail))) {
                revoke.mutate(delegation.id);
              }
            }}
          >
            {t.mcp.held.revoke}
          </Button>
        </div>
      ))}
    </SettingsCard>
  );
}
