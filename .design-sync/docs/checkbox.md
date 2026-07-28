---
category: Forms
---

Binary toggle for a single option (Radix Checkbox). A 4×4 box that fills with `--primary` and shows a check when on. Use for opt-ins and for multi-select filters; use `Select` when the choice is one-of-many.

## Usage

- Always pair with a `Label` — link `htmlFor` ↔ the checkbox `id` so the caption is a click target:

```tsx
<div className="flex items-center gap-2">
  <Checkbox id="stay-signed-in" checked={value} onCheckedChange={next => setValue(next === true)} />
  <Label htmlFor="stay-signed-in" className="font-normal">Stay signed in</Label>
</div>
```

- Row idiom is `flex items-center gap-2` — horizontal, unlike the `Label`-above-`Input` field idiom.
- `font-normal` on the label: a checkbox caption is a sentence, not a field name, so it drops the default medium weight.
- `onCheckedChange` hands you `boolean | "indeterminate"`. Compare with `=== true` rather than coercing, or the indeterminate state reads as checked.
- Stack a set with `grid gap-2` under one `Label` acting as the group caption (see the Group story).
- Explanatory text goes in a sibling `<p className="text-muted-foreground text-sm">`, never inside the label.
