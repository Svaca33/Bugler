---
category: Forms
---

Single-value dropdown (Radix Select). A compound component — always this exact skeleton:

```tsx
<Select value={env} onValueChange={setEnv}>
  <SelectTrigger className="w-44">
    <SelectValue placeholder="All environments" />
  </SelectTrigger>
  <SelectContent>
    <SelectItem value="production">Production</SelectItem>
    <SelectItem value="staging">Staging</SelectItem>
    <SelectItem value="development">Development</SelectItem>
  </SelectContent>
</Select>
```

## Parts

- `Select` — the stateful root: `value`/`onValueChange` (controlled) or `defaultValue`. No DOM of its own.
- `SelectTrigger` — the visible closed control. Width is yours to set (`w-44`, `w-full`); default is fit-content. `size="sm"` (h-8) matches small buttons/inputs in filter bars; default is h-9. `disabled` lives here.
- `SelectValue` — renders the chosen item's text inside the trigger; `placeholder` shows muted text while empty.
- `SelectContent` — the popover (portal, z-50). Children: `SelectItem`s, optionally organized with `SelectGroup` + `SelectLabel` (group heading) and `SelectSeparator`.
- `SelectItem` — one option; `value` required, children are the visible text. Selected item gets a check mark automatically.

## Judgment

- Labels in items should be short; the trigger clamps to one line.
- Filter-bar idiom (from Bugler's Explore pages): small trigger, fixed width, placeholder doubles as the "all" state — `<SelectTrigger className="w-44" size="sm">`.
- For an empty/clearable filter, include a leading item that represents "all" rather than relying on empty value.
- Radix Select requires non-empty `value` strings on every `SelectItem`.
