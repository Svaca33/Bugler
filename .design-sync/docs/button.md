---
category: Actions
---

The single action trigger. Six visual variants and six sizes cover every button need in Bugler — never build a custom button.

## Variants (`variant` prop)

- `default` — solid primary; the one main action of a view (Save, Sign in, Create).
- `destructive` — solid red; irreversible actions (Delete user, Purge logs). Pair with a confirmation step.
- `outline` — bordered, transparent; secondary actions next to a default button (Cancel, Back).
- `secondary` — soft filled; standalone medium-emphasis actions (Refresh, Export).
- `ghost` — no chrome until hover; toolbars, icon buttons, table row actions.
- `link` — text styled as a link; inline navigation-like actions.

## Sizes (`size` prop)

`default` (h-9), `sm` (h-8), `lg` (h-10), and square icon-only sizes `icon`, `icon-sm`, `icon-lg`. Icon-only buttons take exactly one icon child and need an `aria-label`.

## Behavior

- Any `<svg>` icon child is auto-sized to 16px and gap-spaced — just place it before the text: `<Button><PlusIcon />Add app</Button>`.
- `asChild` renders the styles onto the child element instead — use for links that look like buttons: `<Button asChild><a href="…">Docs</a></Button>`.
- `disabled` dims to 50% and blocks pointer events. For async work, disable while pending and keep the label ("Saving…").

## Example

```tsx
<div className="flex items-center gap-2">
  <Button>Save changes</Button>
  <Button variant="outline">Cancel</Button>
  <Button variant="destructive" size="sm">Delete</Button>
</div>
```
