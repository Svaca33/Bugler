import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
} from "@/components/ui/select";

import { useT } from "@/i18n";
import { rangePresets, rangeUnits, toDuration, type RangeUnit } from "@/lib/duration";

import {
  instantToLocalInput,
  localInputToInstant,
  timeFilterLabel,
  type TimeFilterValue,
} from "./timeFilter";

const ALL = "__all__";
const CUSTOM = "__custom__";

/**
 * The Time Filter control: presets in a Select, and a `Custom…` entry that expands the filter bar
 * inline — the way AttributeFilterBar does — because Radix Select cannot hold input fields.
 */
export function TimeFilterControl(props: {
  value: TimeFilterValue;
  onChange: (value: TimeFilterValue) => void;
  /** `row` flows into a wrapping filter bar; `column` stacks full-width inside the filter rail. */
  layout?: "row" | "column";
}) {
  const { value, onChange, layout = "row" } = props;
  const t = useT();
  const words = t.explore.timeFilter;
  const column = layout === "column";
  const [editing, setEditing] = useState(false);
  const [amount, setAmount] = useState("45");
  const [unit, setUnit] = useState<RangeUnit>("minutes");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const presets = rangePresets();
  const units = rangeUnits();
  const preset = presets.find(option => option.value === value.range);
  const isCustom =
    preset === undefined &&
    (value.range !== undefined || value.from !== undefined || value.to !== undefined);

  const fromInstant = localInputToInstant(from);
  const toInstant = localInputToInstant(to);
  const endsBeforeItStarts =
    fromInstant !== undefined && toInstant !== undefined && fromInstant > toInstant;
  const duration = toDuration(Number(amount), unit);

  const openEditor = () => {
    setFrom(instantToLocalInput(value.from));
    setTo(instantToLocalInput(value.to));
    setEditing(true);
  };

  const apply = (next: TimeFilterValue) => {
    onChange(next);
    setEditing(false);
  };

  // Stacked, a label sits above its field; inline it is a gutter the fields line up against.
  // The `contents` wrappers below are boxes the browser skips, so the row layout stays flat.
  const endLabel = column ? "text-muted-foreground text-xs" : "text-muted-foreground w-10 text-xs";

  return (
    <>
      <Select
        value={preset?.value ?? (isCustom ? CUSTOM : ALL)}
        onValueChange={selected => {
          if (selected === CUSTOM) {
            openEditor();
          } else {
            apply(selected === ALL ? {} : { range: selected });
          }
        }}
      >
        <SelectTrigger className={column ? "w-full" : "w-48"} size="sm" aria-label={words.timeRangeAria}>
          <span className="truncate">{timeFilterLabel(value)}</span>
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>{words.allTime}</SelectItem>
          {presets.map(option => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
          <SelectItem value={CUSTOM}>{words.custom}</SelectItem>
        </SelectContent>
      </Select>

      {isCustom && !editing && (
        <Button type="button" variant="ghost" size="sm" className={column ? "w-full" : undefined} onClick={openEditor}>
          {words.edit}
        </Button>
      )}

      {editing && (
        <div className="bg-popover flex flex-col gap-2 rounded-md border p-2">
          <div className={column ? "flex flex-col gap-1.5" : "flex items-center gap-1.5"}>
            <span className={endLabel}>{words.last}</span>
            <span className={column ? "flex gap-1.5" : "contents"}>
              <Input
                type="number"
                min={1}
                className={column ? "h-8 w-16" : "h-8 w-20"}
                aria-label={words.amountAria}
                value={amount}
                onChange={event => setAmount(event.target.value)}
              />
              <Select value={unit} onValueChange={selected => setUnit(selected as RangeUnit)}>
                <SelectTrigger className={column ? "flex-1" : "w-28"} size="sm" aria-label={words.unitAria}>
                  <span>{units.find(option => option.value === unit)?.label}</span>
                </SelectTrigger>
                <SelectContent>
                  {units.map(option => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </span>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              className={column ? "w-full" : undefined}
              aria-label={words.applyRelativeAria}
              disabled={duration === undefined}
              onClick={() => duration !== undefined && apply({ range: duration })}
            >
              {words.apply}
            </Button>
          </div>

          <div className={column ? "flex flex-col gap-1.5" : "flex items-center gap-1.5"}>
            <span className={endLabel}>{words.from}</span>
            {/* Local wall clock in, UTC on the wire — the same time the list shows you. */}
            <Input
              type="datetime-local"
              step={1}
              className={column ? "h-8 w-full" : "h-8 w-52"}
              aria-label={words.from}
              value={from}
              onChange={event => setFrom(event.target.value)}
            />
            {/* Inline the ends read "From … to …"; stacked they are two labelled fields. */}
            <span className="text-muted-foreground text-xs">{column ? words.to : words.toInline}</span>
            <Input
              type="datetime-local"
              step={1}
              className={column ? "h-8 w-full" : "h-8 w-52"}
              aria-label={words.to}
              value={to}
              onChange={event => setTo(event.target.value)}
            />
            <span className={column ? "flex gap-1.5" : "contents"}>
              <Button
                type="button"
                variant="secondary"
                size="sm"
                className={column ? "flex-1" : undefined}
                aria-label={words.applyAbsoluteAria}
                disabled={endsBeforeItStarts}
                onClick={() => apply({ from: fromInstant, to: toInstant })}
              >
                {words.apply}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                aria-label={words.cancelAria}
                onClick={() => setEditing(false)}
              >
                ✕
              </Button>
            </span>
          </div>

          {endsBeforeItStarts && (
            <p className="text-destructive text-xs">{words.endsBeforeItStarts}</p>
          )}
          <p className="text-muted-foreground text-[11px]">{words.hint}</p>
        </div>
      )}
    </>
  );
}
