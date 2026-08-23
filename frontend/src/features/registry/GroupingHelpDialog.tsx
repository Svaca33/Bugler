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

/**
 * The explainer behind the `?` beside "what counts as the same trouble": the ladder the Rule picks
 * a rung of, what a frame is once the noise is stripped, and how far one episode reaches
 * (ADR 0033, 0034). The dropdown alone cannot say any of it, and the reader is a developer — so
 * this shows the stack trace being reduced rather than describing it.
 *
 * Static content; Radix owns the open/close state through the trigger. The prose lives in the
 * catalog (ADR 0024); what is drawn here is the scaffolding and the code sample, which is code.
 */
export function GroupingHelpDialog() {
  const help = useT().registry.groupingHelp;
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
      {/* The cap is an inline style rather than a `sm:max-w-…` utility on purpose: it has the same
          specificity as the kit's own `max-w-[calc(100%-2rem)]`, so which of the two wins comes
          down to the order Tailwind happened to emit the two arbitrary values in. */}
      <DialogContent
        className="flex max-h-[calc(100svh-56px)] w-full flex-col gap-0 overflow-hidden border-[#22394F] bg-card p-0"
        style={{ maxWidth: "min(880px, calc(100% - 2rem))" }}
      >
        <div className="flex shrink-0 flex-col gap-1 border-b border-[#1E344C] px-[22px] pt-4 pb-3.5">
          <DialogTitle className="text-[17px] font-semibold tracking-[-0.3px]">
            {help.title}
          </DialogTitle>
          <DialogDescription>{help.description}</DialogDescription>
        </div>

        <div className="flex min-h-0 flex-1 flex-col gap-[19px] overflow-auto px-[22px] py-[18px]">
          {/* The ladder, finest at the top — the order the recipe tries, and the order the
              dropdown coarsens through. */}
          <section className="flex flex-col gap-[11px]">
            <SectionLabel>{help.ladderLabel}</SectionLabel>
            <div className="grid grid-cols-[92px_1fr] gap-x-[13px]">
              <div className="row-span-4 flex min-w-0 flex-col justify-between border-r border-[#1E344C] pr-[13px] text-right font-mono text-[10px] tracking-[0.1em] text-[#5F7590]">
                <span>{help.finer}</span>
                <span>{help.coarser}</span>
              </div>
              <Rung
                title={help.rungAttributeTitle}
                badge={help.rungAboveTheRule}
                tint="bg-primary/16 text-primary"
              >
                {help.rungAttributeBody}
              </Rung>
              <Rung
                title={help.rungStackTitle}
                badge={help.rungDefault}
                tint="bg-severity-error/16 text-severity-error"
              >
                {help.rungStackBody}
              </Rung>
              <Rung title={help.rungFailureTitle} tint="bg-severity-warn/16 text-severity-warn">
                {help.rungFailureBody}
              </Rung>
              <Rung title={help.rungMessageTitle} tint="bg-[#22394F] text-[#A9BDD1]">
                {help.rungMessageBody}
              </Rung>
            </div>
            <p className="text-[11.5px] leading-[1.5] text-[#8CA1B8]">{help.ruleNote}</p>
            <p className="text-[11.5px] leading-[1.5] text-[#8CA1B8]">{help.degradeNote}</p>
          </section>

          {/* What "the code that threw" actually means, shown rather than described: the header
              carries a hostname and a transaction number, and hashing it would mint a fingerprint
              per occurrence. */}
          <section className="flex flex-col gap-[11px]">
            <SectionLabel>{help.framesLabel}</SectionLabel>
            <div className="grid grid-cols-[1fr_22px_1fr] items-center gap-y-1.5">
              <span className="font-mono text-[10px] tracking-[0.1em] text-[#5F7590]">
                {help.framesRawCaption}
              </span>
              <span />
              <span className="font-mono text-[10px] tracking-[0.1em] text-[#5F7590]">
                {help.framesKeptCaption}
              </span>
              <Sample>{RAW_STACK}</Sample>
              <span className="flex items-center justify-center text-[13px] text-[#3E5570]">→</span>
              <Sample kept>{KEPT_FRAMES}</Sample>
            </div>
            <p className="text-[11.5px] leading-[1.5] text-[#8CA1B8]">{help.framesNote}</p>
            <p className="text-[11.5px] leading-[1.5] text-[#8CA1B8]">{help.runtimesNote}</p>
          </section>

          {/* The other half of the answer: what the fingerprint is compared within. */}
          <section className="flex flex-col gap-[11px]">
            <SectionLabel>{help.scopeLabel}</SectionLabel>
            <p className="text-[12px] leading-[1.5] text-[#A9BDD1]">{help.scopeAlways}</p>
            <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
              <Facet title={help.byNamespace}>{help.scopeNsNote}</Facet>
              <Facet title={help.byEnvironment} badge={help.rungDefault}>
                {help.scopeEnvNote}
              </Facet>
              <Facet title={help.byServiceName}>{help.scopeNameNote}</Facet>
            </div>
            <p className="text-[11.5px] leading-[1.5] text-[#8CA1B8]">{help.scopeExample}</p>
          </section>
        </div>

        <div className="flex shrink-0 items-center gap-4 border-t border-[#1E344C] bg-sidebar px-[22px] py-[13px]">
          <span className="text-[11.5px] text-[#7D93AA]">{help.repartitionNote}</span>
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

/**
 * A real .NET stack trace and what survives it. Code rather than prose, so it is the same in every
 * language — and it is the point of the section: the first line carries a hostname and a
 * transaction number, which is exactly why the frames are hashed and the words are not.
 */
const RAW_STACK = `System.TimeoutException: connect timed out
  to db-07 (txn 41982)
   at Acme.Payments.Charge(Order o)
      in /src/Payments.cs:line 42
   --- End of stack trace from previous
       location ---
   at Acme.Checkout.Handle(Order o)
      in /src/Checkout.cs:line 22`;

const KEPT_FRAMES = `System.TimeoutException

Acme.Payments.Charge(Order o)
Acme.Checkout.Handle(Order o)`;

function SectionLabel({ children }: { children: ReactNode }) {
  return <span className="font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">{children}</span>;
}

function Sample(props: { kept?: boolean; children: ReactNode }) {
  return (
    <pre
      className={`min-w-0 overflow-x-auto rounded-[9px] border px-3 py-2.5 font-mono text-[10.5px] leading-[1.5] whitespace-pre ${
        props.kept === true
          ? "border-[#2A4A38] bg-[#0C1A14] text-[#9FD3B4]"
          : "border-[#1E344C] bg-popover text-[#7D93AA]"
      }`}
    >
      {props.children}
    </pre>
  );
}

function Rung(props: { title: string; badge?: string; tint: string; children: ReactNode }) {
  return (
    <div className="flex min-w-0 flex-col gap-1 border-b border-[#101F31] py-2.5 last:border-b-0">
      <span className="flex items-center gap-2 text-[12.5px] font-semibold">
        {props.title}
        {props.badge !== undefined && (
          <span className={`rounded-[5px] px-[6px] py-px font-mono text-[9.5px] ${props.tint}`}>
            {props.badge}
          </span>
        )}
      </span>
      <span className="text-[11.5px] leading-[1.5] text-[#8CA1B8]">{props.children}</span>
    </div>
  );
}

function Facet(props: { title: string; badge?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-[5px] rounded-[10px] border border-[#22394F] px-3 py-2.5">
      <span className="flex items-center gap-2 text-[12px] font-semibold">
        {props.title}
        {props.badge !== undefined && (
          <span className="rounded-[5px] bg-[#22394F] px-[6px] py-px font-mono text-[9.5px] text-[#A9BDD1]">
            {props.badge}
          </span>
        )}
      </span>
      <span className="text-[11.5px] leading-[1.45] text-[#8CA1B8]">{props.children}</span>
    </div>
  );
}
