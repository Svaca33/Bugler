---
category: Forms
---

Multi-line text entry. A styled native `<textarea>` — all native props work (`placeholder`, `value`, `onChange`, `rows`, `disabled`, `required`…).

## Usage

- Auto-grows with content (`field-sizing-content`) from a 4-line minimum (min-h-16) — no manual `rows` juggling needed; set `rows` only to reserve more initial height.
- Full-width by default; same field idiom as Input:

```tsx
<div className="grid gap-2">
  <Label htmlFor="notes">Notes</Label>
  <Textarea id="notes" placeholder="What happened before the error?" />
</div>
```

## States

Same state contract as Input: `aria-invalid` for error styling, `disabled` dims, focus ring built in.
