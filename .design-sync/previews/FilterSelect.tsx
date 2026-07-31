import { FilterSelect } from "bugler-frontend";

/*
 * FilterSelect is controlled and nullable: `undefined` shows the placeholder, and picking the
 * placeholder row clears back to it. Width belongs to the caller — the rail passes `w-full`,
 * a filter bar passes nothing and gets the default `w-44`.
 */

const ENVIRONMENTS = [
  { value: "production", label: "production" },
  { value: "staging", label: "staging" },
  { value: "dev", label: "dev" },
];

/** Nothing chosen: the placeholder names the whole set, which is what "no filter" means. */
export const Cleared = () => (
  <div className="w-56">
    <FilterSelect
      className="w-full"
      placeholder="All environments"
      value={undefined}
      options={ENVIRONMENTS}
      onChange={() => {}}
    />
  </div>
);

/** A value chosen — the control reads as the active narrowing it is. */
export const Chosen = () => (
  <div className="w-56">
    <FilterSelect
      className="w-full"
      placeholder="All environments"
      value="production"
      options={ENVIRONMENTS}
      onChange={() => {}}
    />
  </div>
);

/** The rail's SOURCE group: facet selects stacked full-width, each clearable on its own. */
export const SourceStack = () => (
  <div className="flex w-56 flex-col gap-2">
    <FilterSelect
      className="w-full"
      placeholder="All applications"
      value="eshop"
      options={[{ value: "eshop", label: "Eshop" }, { value: "crm", label: "Crm" }]}
      onChange={() => {}}
    />
    <FilterSelect
      className="w-full"
      placeholder="All namespaces"
      value={undefined}
      options={[{ value: "acme", label: "acme" }]}
      onChange={() => {}}
    />
    <FilterSelect
      className="w-full"
      placeholder="All services"
      value={undefined}
      options={[{ value: "web", label: "web" }, { value: "worker", label: "worker" }]}
      onChange={() => {}}
    />
  </div>
);
