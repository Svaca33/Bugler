import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

/**
 * The shape every settings page has: a centred column of cards that splits into two once there is
 * room for both, and never lets a card grow wider than a form is comfortable to read.
 *
 * The split is decided by a container query rather than by the window, so the same page keeps the
 * right number of columns wherever it is mounted — a full route today, a panel inside something
 * narrower tomorrow — without any page having to know which it is.
 */
export function SettingsPage(props: {
  title?: string;
  description?: string;
  /** A page inside a shell that already carries the h1 says so; the size follows the level. */
  headingLevel?: 1 | 2;
  /**
   * Two columns the page assigns itself, instead of letting the browser balance one flow. Worth
   * asking for when a card's height is the reader's own doing — a Focus card grows with the number
   * of applications on the server — because a balanced flow would then move the other cards about
   * whenever somebody registered one. Below the split it is a single column, left cards first.
   */
  split?: { left: ReactNode; right: ReactNode };
  children?: ReactNode;
}) {
  const Heading = props.headingLevel === 2 ? "h2" : "h1";
  const hasHeader = props.title !== undefined || props.description !== undefined;

  return (
    <div className="@container/settings h-full min-h-0 overflow-auto">
      <div className="mx-auto flex w-full max-w-[668px] flex-col gap-[18px] px-6 py-5 @min-[1400px]/settings:max-w-[1306px]">
        {hasHeader && (
          <div className="flex flex-col gap-1">
            {props.title !== undefined && (
              <Heading
                className={
                  props.headingLevel === 2
                    ? "text-[17px] font-semibold tracking-[-0.3px]"
                    : "text-[19px] font-semibold tracking-[-0.4px]"
                }
              >
                {props.title}
              </Heading>
            )}
            {props.description !== undefined && (
              <p className="text-[12.5px] text-[#8CA1B8]">{props.description}</p>
            )}
          </div>
        )}

        {/*
          A column flow rather than a grid: a grid aligns its rows, so a short card would hold the
          hole under it open all the way down to the bottom of the tall one beside it. Here each
          column packs its own cards one under the next, always the same step apart, and the browser
          balances the two. The price is that the reading order runs down the first column and then
          down the second — which is what an evenly spaced stack costs.

          The step is the card's own bottom margin (a column flow has no row gap to set), and the
          trailing one below the last card is taken back off the flow's own box.
        */}
        {props.split === undefined ? (
          <div className="-mb-[18px] gap-x-[18px] @min-[1400px]/settings:columns-2">
            {props.children}
          </div>
        ) : (
          /*
            The same step and the same breakpoint, but the columns are the page's to fill. `items-start`
            keeps each one packing from the top rather than stretching to the taller one's height.
          */
          <div className="-mb-[18px] flex flex-col @min-[1400px]/settings:grid @min-[1400px]/settings:grid-cols-2 @min-[1400px]/settings:items-start @min-[1400px]/settings:gap-x-[18px]">
            <div>{props.split.left}</div>
            <div>{props.split.right}</div>
          </div>
        )}
      </div>
    </div>
  );
}

/**
 * One card of a {@link SettingsPage}: the frame, and the caption that names what it settles.
 * It carries no width of its own — the page's column decides that, which is what lets the same
 * card sit in one column or two without being told which — and it is never split across the
 * column break, which is the one thing a column flow would otherwise do to it.
 */
export function SettingsCard(props: {
  caption?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <section
      className={cn(
        "mb-[18px] flex break-inside-avoid flex-col gap-4 rounded-[11px] border border-[#1E344C] bg-card p-4",
        props.className,
      )}
    >
      {props.caption !== undefined && (
        <span className="font-mono text-[11px] tracking-[0.08em] text-[#7D93AA]">
          {props.caption}
        </span>
      )}
      {props.children}
    </section>
  );
}
