import { Checkbox, FilterGroup, FilterRail, FilterSelect, Input, Label } from "bugler-frontend";

/*
 * The rail is 254px wide by its own decision and fills the height its parent gives it — every
 * story stands it in a flex row with a definite height, exactly as the app does. Controls are
 * full width; nothing in the rail scrolls sideways.
 *
 * Stories render inside `.dark`: the rail's chrome colours are tuned for the navy console theme
 * the app pins itself to, and on the light theme its foreground text would sit dark-on-navy.
 */

/** The Alerts triage rail: checkbox groups with counts, facet selects, a submit-on-Enter text filter. */
export const Triage = () => (
  <div className="dark flex h-96 w-full overflow-hidden rounded-md border bg-background text-foreground">
    <FilterRail canClear onClear={() => {}}>
      <FilterGroup label="LIFECYCLE">
        {[
          { name: "Open", count: 3, hot: true },
          { name: "Quieted", count: 14 },
          { name: "Solved", count: 126 },
        ].map(state => (
          <div key={state.name} className="flex items-center gap-2">
            <Checkbox id={`rail-${state.name}`} checked onCheckedChange={() => {}} />
            <Label htmlFor={`rail-${state.name}`} className="font-normal text-sm">
              {state.name}
            </Label>
            <span
              className={`ml-auto font-mono text-xs ${
                state.hot === true ? "text-severity-error" : "text-muted-foreground"
              }`}
            >
              {state.count}
            </span>
          </div>
        ))}
      </FilterGroup>
      <FilterGroup label="SOURCE">
        <FilterSelect
          className="w-full"
          placeholder="All applications"
          value={undefined}
          options={[{ value: "eshop", label: "Eshop" }]}
          onChange={() => {}}
        />
        <FilterSelect
          className="w-full"
          placeholder="All environments"
          value="production"
          options={[{ value: "production", label: "production" }]}
          onChange={() => {}}
        />
      </FilterGroup>
      <FilterGroup label="FIRST LOG CONTAINS">
        <Input placeholder="timeout, deadlock…" />
      </FilterGroup>
    </FilterRail>
    <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
      list
    </div>
  </div>
);

/** Nothing narrowed yet: Clear all sits disabled until any control writes a filter. */
export const Untouched = () => (
  <div className="dark flex h-56 w-full overflow-hidden rounded-md border bg-background text-foreground">
    <FilterRail canClear={false} onClear={() => {}}>
      <FilterGroup label="SOURCE">
        <FilterSelect
          className="w-full"
          placeholder="All services"
          value={undefined}
          options={[{ value: "web", label: "web" }]}
          onChange={() => {}}
        />
      </FilterGroup>
      <FilterGroup label="MESSAGE">
        <Input placeholder="Search in message…" />
      </FilterGroup>
    </FilterRail>
    <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
      list
    </div>
  </div>
);
