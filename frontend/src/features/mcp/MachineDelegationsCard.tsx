import { useState } from "react";

import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SettingsCard } from "@/components/ui/settings-layout";
import { getFormatLocale, useT } from "@/i18n";

import { IssuedMachineDelegationPanel } from "./IssuedMachineDelegationPanel";
import {
  type IssuedMachineDelegation,
  type MachineDelegationGrade,
  useIssueMachineDelegation,
  useMcpConnection,
  useOwnMachineDelegations,
  useRevokeMachineDelegation,
} from "./useMachineDelegations";

const LIFETIMES = [30, 90, 365];

const ALL = "all";

/**
 * Your own Machine Delegations: what a tool on your machine may read in your name. Nothing here can widen
 * your Visibility Scope — a delegation is your reading lent out, never an identity of its own
 * (ADR 0029).
 */
export function MachineDelegationsCard() {
  const t = useT();
  const locale = getFormatLocale();
  const connection = useMcpConnection();
  const delegations = useOwnMachineDelegations();
  const catalog = useCatalog();
  const issue = useIssueMachineDelegation();
  const revoke = useRevokeMachineDelegation();

  const [name, setName] = useState("");
  const [application, setApplication] = useState<string>(ALL);
  const [lifetime, setLifetime] = useState(90);
  const [grade, setGrade] = useState<MachineDelegationGrade>("Reading");
  const [issued, setIssued] = useState<IssuedMachineDelegation | null>(null);

  const date = (value: string | null | undefined) =>
    value === null || value === undefined
      ? null
      : new Date(value).toLocaleString(locale, { dateStyle: "medium", timeStyle: "short" });

  const applicationName = (id: string | null | undefined) =>
    id === null || id === undefined
      ? t.mcp.list.scopeEverything
      : (catalog.data?.applications.find(a => a.id === id)?.name ?? id);

  const closed = connection.data?.opened === false;

  return (
    <SettingsCard caption={t.mcp.page.title}>
      <p className="text-[12.5px] text-[#8CA1B8]">{t.mcp.page.subtitle}</p>

      {closed && (
        <p className="rounded-[9px] border border-[#3A2E1A] bg-[#1B1508] px-3.5 py-3 text-[12.5px] text-[#D8B76A]">
          {t.mcp.page.doorClosed}
        </p>
      )}

      {connection.data?.opened === true && connection.data.publicUrl.length === 0 && (
        <p className="rounded-[9px] border border-[#3A2E1A] bg-[#1B1508] px-3.5 py-3 text-[12.5px] text-[#D8B76A]">
          {t.mcp.page.addressMissing}
        </p>
      )}

      {issued !== null ? (
        <IssuedMachineDelegationPanel
          issued={issued}
          publicUrl={connection.data?.publicUrl ?? ""}
          onDone={() => {
            setIssued(null);
            setName("");
            setApplication(ALL);
          }}
        />
      ) : (
        <form
          className="grid gap-3.5 border-t border-[#17293D] pt-4"
          onSubmit={event => {
            event.preventDefault();
            if (name.trim().length === 0) return;
            issue.mutate(
              {
                name: name.trim(),
                applicationId: application === ALL ? null : application,
                lifetimeDays: lifetime,
                grade,
              },
              { onSuccess: setIssued },
            );
          }}
        >
          <div className="grid gap-1.5">
            <Label htmlFor="delegation-name">{t.mcp.issue.nameLabel}</Label>
            <Input
              id="delegation-name"
              value={name}
              maxLength={100}
              placeholder={t.mcp.issue.namePlaceholder}
              onChange={event => setName(event.target.value)}
            />
          </div>

          <div className="grid gap-3.5 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="delegation-application">{t.mcp.issue.applicationLabel}</Label>
              <Select value={application} onValueChange={setApplication}>
                <SelectTrigger id="delegation-application" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>{t.mcp.issue.applicationAll}</SelectItem>
                  {(catalog.data?.applications ?? []).map(app => (
                    <SelectItem key={app.id} value={app.id}>
                      {app.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="delegation-lifetime">{t.mcp.issue.lifetimeLabel}</Label>
              <Select value={String(lifetime)} onValueChange={value => setLifetime(Number(value))}>
                <SelectTrigger id="delegation-lifetime" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {LIFETIMES.map(days => (
                    <SelectItem key={days} value={String(days)}>
                      {t.mcp.issue.lifetimeOption(days)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="delegation-grade">{t.mcp.grade.label}</Label>
            <Select
              value={grade}
              onValueChange={value => setGrade(value as MachineDelegationGrade)}
            >
              <SelectTrigger id="delegation-grade" className="w-full sm:w-[calc(50%-7px)]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Reading">{t.mcp.grade.reading}</SelectItem>
                <SelectItem value="MachineHand">{t.mcp.grade.machineHand}</SelectItem>
              </SelectContent>
            </Select>
            <p className="text-[11.5px] text-[#7D93AA]">{t.mcp.grade.hint}</p>
          </div>

          {issue.isError && <p className="text-[12.5px] text-destructive">{issue.error.message}</p>}

          <div>
            <Button type="submit" disabled={closed || issue.isPending || name.trim().length === 0}>
              {issue.isPending ? t.mcp.issue.submitting : t.mcp.issue.submit}
            </Button>
          </div>
        </form>
      )}

      <div className="flex flex-col gap-3 border-t border-[#17293D] pt-4">
        {delegations.data?.length === 0 && (
          <p className="text-[12.5px] text-[#8CA1B8]">{t.mcp.list.empty}</p>
        )}

        {(delegations.data ?? []).map(delegation => (
          <div key={delegation.id} className="flex items-center gap-4">
            <div className="flex min-w-0 flex-1 flex-col gap-0.5">
              <span className="truncate text-[13px] text-[#DCE8F3]">{delegation.name}</span>
              <span className="truncate text-[11.5px] text-[#8CA1B8]">
                {applicationName(delegation.applicationId)}
              </span>
            </div>

            <dl className="hidden gap-5 text-[11.5px] sm:flex">
              <div className="flex flex-col gap-0.5">
                <dt className="text-[#7D93AA]">{t.mcp.grade.column}</dt>
                <dd className="text-[#A9BDD1]">
                  {delegation.grade === "MachineHand"
                    ? t.mcp.grade.machineHand
                    : t.mcp.grade.reading}
                </dd>
              </div>
              <div className="flex flex-col gap-0.5">
                <dt className="text-[#7D93AA]">{t.mcp.list.lastUsedColumn}</dt>
                <dd className="font-mono text-[#A9BDD1]">
                  {date(delegation.lastUsedAt) ?? t.mcp.list.neverUsed}
                </dd>
              </div>
              <div className="flex flex-col gap-0.5">
                <dt className="text-[#7D93AA]">{t.mcp.list.expiresColumn}</dt>
                <dd className="font-mono text-[#A9BDD1]">{date(delegation.expiresAt)}</dd>
              </div>
            </dl>

            <Button
              variant="outline"
              size="sm"
              disabled={revoke.isPending}
              onClick={() => {
                if (window.confirm(t.mcp.list.revokeConfirm(delegation.name))) {
                  revoke.mutate(delegation.id);
                }
              }}
            >
              {t.mcp.list.revoke}
            </Button>
          </div>
        ))}
      </div>
    </SettingsCard>
  );
}
