import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
} from "@/components/ui/select";

import {
  instantToLocalInput,
  localInputToInstant,
  RANGE_PRESETS,
  RANGE_UNITS,
  timeFilterLabel,
  toDuration,
  type RangeUnit,
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
}) {
  const { value, onChange } = props;
  const [editing, setEditing] = useState(false);
  const [amount, setAmount] = useState("45");
  const [unit, setUnit] = useState<RangeUnit>("minutes");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const preset = RANGE_PRESETS.find(option => option.value === value.range);
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
        <SelectTrigger className="w-48" size="sm" aria-label="Time range">
          <span className="truncate">{timeFilterLabel(value)}</span>
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>All time</SelectItem>
          {RANGE_PRESETS.map(option => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
          <SelectItem value={CUSTOM}>Custom…</SelectItem>
        </SelectContent>
      </Select>

      {isCustom && !editing && (
        <Button type="button" variant="ghost" size="sm" onClick={openEditor}>
          Edit
        </Button>
      )}

      {editing && (
        <div className="bg-popover flex flex-col gap-2 rounded-md border p-2">
          <div className="flex items-center gap-1.5">
            <span className="text-muted-foreground w-10 text-xs">Last</span>
            <Input
              type="number"
              min={1}
              className="h-8 w-20"
              aria-label="Amount"
              value={amount}
              onChange={event => setAmount(event.target.value)}
            />
            <Select value={unit} onValueChange={selected => setUnit(selected as RangeUnit)}>
              <SelectTrigger className="w-28" size="sm" aria-label="Unit">
                <span>{RANGE_UNITS.find(option => option.value === unit)?.label}</span>
              </SelectTrigger>
              <SelectContent>
                {RANGE_UNITS.map(option => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              aria-label="Apply relative range"
              disabled={duration === undefined}
              onClick={() => duration !== undefined && apply({ range: duration })}
            >
              Apply
            </Button>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-muted-foreground w-10 text-xs">From</span>
            {/* Local wall clock in, UTC on the wire — the same time the list shows you. */}
            <Input
              type="datetime-local"
              step={1}
              className="h-8 w-52"
              aria-label="From"
              value={from}
              onChange={event => setFrom(event.target.value)}
            />
            <span className="text-muted-foreground text-xs">to</span>
            <Input
              type="datetime-local"
              step={1}
              className="h-8 w-52"
              aria-label="To"
              value={to}
              onChange={event => setTo(event.target.value)}
            />
            <Button
              type="button"
              variant="secondary"
              size="sm"
              aria-label="Apply absolute range"
              disabled={endsBeforeItStarts}
              onClick={() => apply({ from: fromInstant, to: toInstant })}
            >
              Apply
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              aria-label="Cancel time range"
              onClick={() => setEditing(false)}
            >
              ✕
            </Button>
          </div>

          {endsBeforeItStarts && (
            <p className="text-destructive text-xs">This range ends before it starts.</p>
          )}
          <p className="text-muted-foreground text-[11px]">
            Empty ends stay open. Times are in your local time zone.
          </p>
        </div>
      )}
    </>
  );
}
