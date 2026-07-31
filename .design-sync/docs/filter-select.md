---
category: Forms
---

A nullable single-select for filters: choosing the placeholder entry clears the filter, so "nothing chosen" and "all of them" are the same honest state. It wraps `Select` — reach for `Select` directly when a value is required, and for `Combobox` when the option set needs searching.

## Usage

- Controlled and nullable: `value` is `string | undefined`, and `onChange` receives `undefined` when the reader picks the placeholder row back:

```tsx
<FilterSelect
  className="w-full"
  placeholder="All environments"
  value={filters.environment}
  options={environments.map(e => ({ value: e, label: e }))}
  onChange={environment => setFilters({ ...filters, environment })}
/>
```

- The `placeholder` names the whole set ("All applications", "Any time") — it doubles as the clear entry at the top of the options, so word it as the unfiltered state, never as an imperative.
- Width belongs to the caller: the filter rail passes `className="w-full"`, a filter bar passes nothing and gets the default `w-44`.
- Options are `{ value, label }`; keep values stable ids and labels human. Dependent facets (picking an application narrows namespaces) are the caller's job — recompute `options` and clear the dependents in `onChange`.
