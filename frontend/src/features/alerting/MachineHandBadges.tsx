import type { Episode } from "@/api/client";
import { useT } from "@/i18n";

/**
 * The machine hand's marks where Episodes are listed (Alerting CONTEXT.md: Machine Claim,
 * Solved Proposal, Resignation) — so a machine-held episode, a proposal awaiting a verdict, or
 * a resignation calling for a human hand is never a surprise found only in the detail panel.
 * Nothing at all while no mark stands.
 */
export function MachineHandBadges(props: { episode: Episode }) {
  const t = useT();
  const { machineClaim, solvedProposal, resignation } = props.episode;

  return (
    <>
      {machineClaim != null && (
        <span
          className="flex-none rounded-sm border border-[#2C4159] px-[5px] text-[#A9BDD1]"
          title={t.alerting.machine.badgeClaimedTitle(
            machineClaim.by.name ?? t.alerting.machine.formerHand,
          )}
        >
          {t.alerting.machine.badgeClaimed}
        </span>
      )}
      {solvedProposal != null && !solvedProposal.overtaken && (
        <span
          className="flex-none rounded-sm border border-[rgba(233,164,60,0.55)] px-[5px] text-primary"
          title={t.alerting.machine.badgeProposalTitle}
        >
          {t.alerting.machine.badgeProposal}
        </span>
      )}
      {resignation != null && !resignation.overtaken && (
        <span
          className="flex-none rounded-sm border border-[rgba(201,123,18,0.5)] px-[5px] text-severity-warn"
          title={t.alerting.machine.badgeResignedTitle}
        >
          {t.alerting.machine.badgeResigned}
        </span>
      )}
    </>
  );
}
