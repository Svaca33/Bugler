import { useT } from "@/i18n";

import { ALL_FACETS, type Facet } from "./serviceOverview";

/** The three windows the board offers — labelled by the shared range presets. */
const WINDOW_VALUES = ["PT1H", "P1D", "P7D"];

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";
const SHELL = "flex gap-0.5 rounded-[7px] border border-[#1E344C] bg-[#12253A] p-0.5";

/** Every control writes the URL; none holds local state. */
export function DashboardControls(props: {
  range: string;
  layout: "cards" | "table";
  trouble: boolean;
  showVolume: boolean;
  groupBy: Facet[];
  shown: number;
  onRange: (range: string) => void;
  onLayout: (layout: "cards" | "table") => void;
  onTrouble: (trouble: boolean) => void;
  onVolume: (show: boolean) => void;
  onGroupBy: (facets: Facet[]) => void;
}) {
  const t = useT();
  return (
    <div className="flex h-11 shrink-0 items-center gap-2.5 border-b border-[#17293D] bg-background px-6">
      <span className={CAPTION}>{t.overview.controls.windowCaption}</span>
      <div className={SHELL}>
        {WINDOW_VALUES.map(value => (
          <button
            key={value}
            type="button"
            className={`rounded-[5px] px-2.5 py-1 font-mono text-[11px] whitespace-nowrap ${
              props.range === value
                ? "bg-[#1B3049] text-[#F6C170]"
                : "text-[#8CA1B8] hover:text-foreground"
            }`}
            onClick={() => props.onRange(value)}
          >
            {t.common.rangePreset[value] ?? value}
          </button>
        ))}
      </div>

      <button
        type="button"
        className={`rounded-md px-[11px] py-1 font-mono text-[11px] whitespace-nowrap ${
          props.trouble
            ? "border border-[rgba(229,84,74,0.3)] bg-[rgba(229,84,74,0.12)] text-[#F0685A]"
            : "border border-[#1E344C] text-[#8CA1B8] hover:border-[#2A415C] hover:text-foreground"
        }`}
        onClick={() => props.onTrouble(!props.trouble)}
      >
        {t.overview.controls.troubleOnly}
      </button>

      <button
        type="button"
        className={`rounded-md px-[11px] py-1 font-mono text-[11px] whitespace-nowrap ${
          props.showVolume
            ? "border border-[#2A415C] bg-[#1B3049] text-[#F6C170]"
            : "border border-[#1E344C] text-[#8CA1B8] hover:border-[#2A415C] hover:text-foreground"
        }`}
        onClick={() => props.onVolume(!props.showVolume)}
      >
        {t.overview.controls.volume}
      </button>

      {/* Cards only: the table is one flat list by design. */}
      {props.layout === "cards" && (
        <>
          <span className={CAPTION}>{t.overview.controls.groupByCaption}</span>
          {ALL_FACETS.map(facet => {
            const active = props.groupBy.includes(facet);
            return (
              <button
                key={facet}
                type="button"
                className={`rounded-md px-2.5 py-[3px] font-mono text-[11px] ${
                  active
                    ? "border border-[#2A415C] bg-[#1B3049] text-[#F6C170]"
                    : "border border-[#1E344C] text-[#8CA1B8]"
                }`}
                onClick={() =>
                  props.onGroupBy(
                    active
                      ? props.groupBy.filter(f => f !== facet)
                      : ALL_FACETS.filter(f => f === facet || props.groupBy.includes(f)),
                  )
                }
              >
                {t.overview.controls.facet[facet]}
              </button>
            );
          })}
        </>
      )}

      <div className="ml-auto flex items-center gap-2.5">
        <span className="font-mono text-[11px] text-[#6E86A0]">
          {t.overview.controls.shown(props.shown)}
        </span>
        <div className={SHELL}>
          <button
            type="button"
            title={t.overview.controls.cardsTitle}
            className={`flex h-6 w-7 items-center justify-center rounded-[5px] ${
              props.layout === "cards"
                ? "bg-[#1B3049] text-[#F6C170]"
                : "text-[#8CA1B8] hover:bg-[#16283C] hover:text-foreground"
            }`}
            onClick={() => props.onLayout("cards")}
          >
            <CardsIcon />
          </button>
          <button
            type="button"
            title={t.overview.controls.tableTitle}
            className={`flex h-6 w-7 items-center justify-center rounded-[5px] ${
              props.layout === "table"
                ? "bg-[#1B3049] text-[#F6C170]"
                : "text-[#8CA1B8] hover:bg-[#16283C] hover:text-foreground"
            }`}
            onClick={() => props.onLayout("table")}
          >
            <TableIcon />
          </button>
        </div>
      </div>
    </div>
  );
}

function CardsIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor" aria-hidden="true">
      <rect x="0" y="0" width="5" height="5" rx="1" />
      <rect x="7" y="0" width="5" height="5" rx="1" />
      <rect x="0" y="7" width="5" height="5" rx="1" />
      <rect x="7" y="7" width="5" height="5" rx="1" />
    </svg>
  );
}

function TableIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor" aria-hidden="true">
      <rect x="0" y="1" width="12" height="2" rx="1" />
      <rect x="0" y="5" width="12" height="2" rx="1" />
      <rect x="0" y="9" width="12" height="2" rx="1" />
    </svg>
  );
}
