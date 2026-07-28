import * as React from "react";

import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

export interface ComboboxOption {
  value: string;
  label: string;
  group?: string;
}

function Combobox(props: {
  options: ComboboxOption[];
  onSelect: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  emptyText?: string;
  autoFocus?: boolean;
  className?: string;
}) {
  const { options, onSelect, onBlur, placeholder, emptyText = "No matches.", autoFocus, className } = props;
  const [query, setQuery] = React.useState("");
  const [open, setOpen] = React.useState(false);
  const [active, setActive] = React.useState(0);
  const listRef = React.useRef<HTMLDivElement>(null);
  const listId = React.useId();

  const filtered = query
    ? options.filter(option => option.label.toLowerCase().includes(query.toLowerCase()))
    : options;

  React.useEffect(() => {
    listRef.current
      ?.querySelector('[data-active="true"]')
      ?.scrollIntoView({ block: "nearest" });
  }, [active, open]);

  const select = (option: ComboboxOption) => {
    onSelect(option.value);
    setQuery("");
    setOpen(false);
  };

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        return;
      }
      setActive(index =>
        event.key === "ArrowDown" ? Math.min(index + 1, filtered.length - 1) : Math.max(index - 1, 0),
      );
    } else if (event.key === "Enter") {
      event.preventDefault();
      const option = filtered[active];
      if (open && option !== undefined) select(option);
    } else if (event.key === "Escape") {
      setOpen(false);
    }
  };

  let previousGroup: string | undefined;

  return (
    <div data-slot="combobox" className={cn("relative", className)}>
      <Input
        data-slot="combobox-input"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        autoFocus={autoFocus}
        placeholder={placeholder}
        value={query}
        onChange={event => {
          setQuery(event.target.value);
          setActive(0);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => {
          setOpen(false);
          onBlur?.();
        }}
        onKeyDown={onKeyDown}
      />
      {open && (
        <div
          data-slot="combobox-content"
          id={listId}
          role="listbox"
          ref={listRef}
          className="bg-popover text-popover-foreground absolute top-full left-0 z-50 mt-1 max-h-64 w-full min-w-[12rem] overflow-y-auto rounded-md border p-1 shadow-md"
        >
          {filtered.length === 0 && (
            <p className="text-muted-foreground px-2 py-1.5 text-sm">{emptyText}</p>
          )}
          {filtered.map((option, index) => {
            const groupLabel =
              option.group !== undefined && option.group !== previousGroup ? option.group : undefined;
            previousGroup = option.group;
            return (
              <React.Fragment key={option.value}>
                {groupLabel !== undefined && (
                  <div
                    data-slot="combobox-group-label"
                    className="text-muted-foreground px-2 py-1.5 text-xs"
                  >
                    {groupLabel}
                  </div>
                )}
                <div
                  data-slot="combobox-item"
                  role="option"
                  aria-selected={index === active}
                  data-active={index === active}
                  className={cn(
                    "flex w-full cursor-default items-center rounded-sm px-2 py-1.5 text-sm outline-hidden select-none",
                    index === active && "bg-accent text-accent-foreground",
                  )}
                  onMouseDown={event => event.preventDefault()}
                  onMouseEnter={() => setActive(index)}
                  onClick={() => select(option)}
                >
                  {option.label}
                </div>
              </React.Fragment>
            );
          })}
        </div>
      )}
    </div>
  );
}

export { Combobox };
