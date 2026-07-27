---
category: Forms
---

Form field caption (Radix Label). Small, medium-weight, one line. Use for every form control — never a bare `<span>`.

## Usage

- Link to the control with `htmlFor` ↔ the control's `id`; clicking the label then focuses the control.
- Field idiom: label above control, `grid gap-2` (see Input).
- It's a flex row with gap-2 — an inline icon or a muted hint can sit inside:

```tsx
<Label htmlFor="retention">
  Retention window
  <span className="text-muted-foreground font-normal">(days)</span>
</Label>
```

- When the paired control is disabled, give the control the `peer` class or wrap the group with `data-disabled` — the label auto-dims. In practice: disabling the control alone is usually enough visually.
