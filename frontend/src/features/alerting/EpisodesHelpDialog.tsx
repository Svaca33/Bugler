import type { ReactNode } from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { useT } from "@/i18n";

import { StateBadge } from "./StateBadge";

/**
 * The first-read explainer behind the `?` next to the page title: the rules the list cannot
 * show — how a kind of trouble is distilled and how far one episode reaches (ADR 0033, 0034),
 * the quiet window, and what Acknowledge suppresses.
 * Static content only; Radix owns the open/close state through the trigger. The prose — with
 * its inline emphasis — lives in the catalog; only the diagram scaffolding is drawn here.
 */
export function EpisodesHelpDialog() {
  const help = useT().alerting.help;
  return (
    <Dialog>
      <DialogTrigger asChild>
        <button
          type="button"
          aria-label={help.title}
          title={help.title}
          className="inline-flex size-[19px] items-center justify-center rounded-full border border-[#2C4159] text-[11.5px] font-semibold text-[#8CA1B8] hover:border-[#E9A43C] hover:text-[#F6C170]"
        >
          ?
        </button>
      </DialogTrigger>
      <DialogContent className="flex max-h-[calc(100svh-56px)] w-full flex-col gap-0 overflow-hidden border-[#22394F] bg-card p-0 sm:max-w-[920px]">
        <div className="flex shrink-0 flex-col gap-1 border-b border-[#1E344C] px-[22px] pt-4 pb-3.5">
          <DialogTitle className="text-[17px] font-semibold tracking-[-0.3px]">
            {help.title}
          </DialogTitle>
          <DialogDescription>{help.description}</DialogDescription>
        </div>

        <div className="flex min-h-0 flex-1 flex-col gap-[19px] overflow-auto px-[22px] py-[18px]">
          <section className="flex flex-col gap-[11px]">
            <SectionLabel>{help.fromTroubleLabel}</SectionLabel>
            <div className="grid grid-cols-[1fr_18px_1fr_18px_1fr_18px_1fr] items-stretch">
              <StepCard number={1} tint="bg-severity-error/16 text-severity-error" title={help.step1Title}>
                {help.step1Body}
              </StepCard>
              <Arrow />
              <StepCard number={2} tint="bg-severity-warn/16 text-severity-warn" title={help.step2Title}>
                {help.step2Body}
              </StepCard>
              <Arrow />
              <StepCard number={3} tint="bg-severity-warn/16 text-severity-warn" title={help.step3Title}>
                {help.step3Body}
              </StepCard>
              <Arrow />
              <StepCard number={4} tint="bg-state-solved/16 text-state-solved" title={help.step4Title}>
                {help.step4Body}
              </StepCard>
            </div>
          </section>

          <section className="flex flex-col gap-3.5">
            <SectionLabel>{help.howItEndsLabel}</SectionLabel>

            <div className="grid grid-cols-[158px_1fr] items-center gap-[18px]">
              <LaneLabel title={help.lane1Title} subtitle={help.lane1Subtitle} />
              <div className="relative h-16">
                <span className="absolute inset-x-0 top-8 h-px bg-[#152840]" />
                <Tick left="1%" />
                <Tick left="4.5%" />
                <Tick left="7.5%" amber />
                <Tick left="11%" />
                <Tick left="14.5%" />
                <Tick left="18%" />
                <span className="absolute top-[27px] left-0 h-[11px] w-[21%] rounded-md bg-[#E5544A]" />
                <span className="absolute top-[27px] left-[21%] h-[11px] w-[31%] rounded-r-md border border-l-0 border-dashed border-[#E5544A]/50 bg-[#E5544A]/12" />
                <span className="absolute top-[5px] left-[21%] w-[31%] text-center font-mono text-[10px] text-[#7D93AA]">
                  {help.lane1WindowNote}
                </span>
                <span className="absolute top-[26px] left-[53.5%] rounded-[5px] border border-[#E9A43C]/42 bg-[#E9A43C]/14 px-[7px] py-0.5 font-mono text-[9.5px] tracking-[0.08em] whitespace-nowrap text-[#E9A43C]">
                  {help.quietedMark}
                </span>
                <Tick left="73%" />
                <Tick left="76.5%" />
                <Tick left="80%" />
                <Tick left="83.5%" />
                <span className="absolute top-[27px] left-[72%] h-[11px] w-[28%] rounded-md bg-[#E5544A]" />
                <span className="absolute top-11 left-[66%] w-[34%] text-center font-mono text-[10px] text-severity-error">
                  {help.lane1NewEpisode}
                </span>
              </div>
            </div>

            <div className="grid grid-cols-[158px_1fr] items-center gap-[18px]">
              <LaneLabel title={help.lane2Title} subtitle={help.lane2Subtitle} />
              <div className="relative h-16">
                <span className="absolute inset-x-0 top-8 h-px bg-[#152840]" />
                <Tick left="1%" />
                <Tick left="4.5%" />
                <Tick left="8%" />
                <Tick left="11.5%" />
                {/* The fade at the right edge says "still running" — this bar has no end cap. */}
                <span className="absolute top-[27px] inset-x-0 h-[11px] rounded-md bg-[linear-gradient(90deg,#E5544A_0%,#E5544A_86%,rgba(229,84,74,0.14)_100%)]" />
                <span className="absolute top-5 left-[22%] h-[25px] w-0.5 bg-[#E9A43C]" />
                <span className="absolute top-[29px] left-[calc(22%-3px)] size-2 rounded-full bg-[#E9A43C] shadow-[0_0_0_2px_var(--card)]" />
                <span className="absolute top-0.5 left-[22%] pl-[7px] font-mono text-[10px] whitespace-nowrap text-[#F6C170]">
                  {help.lane2TookOn}
                </span>
                <span className="absolute top-11 left-[26%] w-[30%] text-center font-mono text-[10px] text-[#7D93AA]">
                  {help.lane2WouldHaveQuieted}
                </span>
                <Tick left="60%" />
                <Tick left="63.5%" />
                <Tick left="67%" amber />
                <Tick left="70.5%" />
                <Tick left="74%" />
                <Tick left="77.5%" />
                <span className="absolute top-11 left-[58%] w-[42%] text-center font-mono text-[10px] text-[#8CA1B8]">
                  {help.lane2SameEpisode}
                </span>
              </div>
            </div>
          </section>

          <section className="flex flex-col gap-[11px]">
            <SectionLabel>{help.fourStatesLabel}</SectionLabel>
            <div className="grid grid-cols-4 gap-2.5">
              <StateCard state="Open" tint="bg-[#E5544A]/7">{help.stateOpenBody}</StateCard>
              <StateCard state="Quieted" tint="bg-[#E9A43C]/6">{help.stateQuietedBody}</StateCard>
              <StateCard state="Solved" tint="bg-[#5FB37C]/6">{help.stateSolvedBody}</StateCard>
              <StateCard state="Muted" tint="bg-[#101F31]">{help.stateMutedBody}</StateCard>
            </div>
            <p className="text-[11.5px] leading-[1.5] text-[#8CA1B8]">{help.actionsSummary}</p>
          </section>
        </div>

        <div className="flex shrink-0 items-center gap-4 border-t border-[#1E344C] bg-sidebar px-[22px] py-[13px]">
          <span className="text-[11.5px] text-[#7D93AA]">{help.footer}</span>
          <DialogClose asChild>
            <Button size="sm" className="ml-auto">
              {help.gotIt}
            </Button>
          </DialogClose>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function SectionLabel({ children }: { children: ReactNode }) {
  return <span className="font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">{children}</span>;
}

function Arrow() {
  return <span className="flex items-center justify-center text-[13px] text-[#3E5570]">→</span>;
}

function StepCard(props: { number: number; tint: string; title: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5 rounded-[10px] border border-[#1E344C] bg-popover px-3 py-[11px]">
      <span className="flex items-center gap-2 text-[12.5px] font-semibold">
        <span
          className={`inline-flex size-4 items-center justify-center rounded-full font-mono text-[10px] ${props.tint}`}
        >
          {props.number}
        </span>
        {props.title}
      </span>
      <span className="text-[11.5px] leading-[1.45] text-[#8CA1B8]">{props.children}</span>
    </div>
  );
}

function LaneLabel(props: { title: string; subtitle: string }) {
  return (
    <div className="flex flex-col gap-0.5 text-right">
      <span className="text-[12.5px] font-semibold">{props.title}</span>
      <span className="text-[11px] leading-[1.4] text-[#8CA1B8]">{props.subtitle}</span>
    </div>
  );
}

/** One matching log on a timeline; amber marks the odd warning among the errors. */
function Tick(props: { left: string; amber?: boolean }) {
  return (
    <span
      className={`absolute top-3.5 h-[11px] w-0.5 rounded-[1px] ${props.amber ? "bg-[#E9A43C]" : "bg-[#E5544A]"}`}
      style={{ left: props.left }}
    />
  );
}

function StateCard(props: {
  state: "Open" | "Quieted" | "Solved" | "Muted";
  tint: string;
  children: ReactNode;
}) {
  return (
    <div className={`flex flex-col gap-[5px] rounded-[10px] border border-[#22394F] px-3 py-2.5 ${props.tint}`}>
      <StateBadge state={props.state} />
      <span className="text-[11.5px] leading-[1.45] text-[#8CA1B8]">{props.children}</span>
    </div>
  );
}
