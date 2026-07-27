---
category: Forms
---

Single-line text entry. A styled native `<input>` — every native prop works (`type`, `placeholder`, `value`, `onChange`, `disabled`, `required`, `maxLength`…).

## Usage

- Full-width by default (w-full, h-9) — control width via the wrapping element, not the input.
- Always pair with a `Label` linked by `htmlFor`/`id`. Stack them with `grid gap-2` (Bugler's field idiom):

```tsx
<div className="grid gap-2">
  <Label htmlFor="app-name">Application name</Label>
  <Input id="app-name" placeholder="checkout-service" />
</div>
```

- `type` covers text, email, password, number, search, file, date… File inputs get styled file-button text automatically.

## States

- **Error**: set `aria-invalid` — the border and focus ring turn destructive. Put the message below in `text-destructive text-sm`.
- **Disabled**: `disabled` dims to 50% and blocks interaction.
- Focus ring is built in (ring around the border) — never add custom focus styles.
