---
category: Forms
---

Compact removable tag for one active filter criterion — mono type, secondary background, optional ✕ button. Bugler renders one per active attribute filter in the Explore filter bars.

```tsx
<FilterChip onRemove={() => removeFilter(f)}>tenant.id = acme</FilterChip>
```

## Behavior

- Children are the chip content (rendered in a truncating span — long values ellipsize; constrain width with `className`/parent width).
- `onRemove` present → a small ✕ button renders after the content; omit it for a static, non-removable chip.
- `removeLabel` sets the ✕ button's aria-label (default "Remove filter").

## Judgment

- Content idiom is `key = value` in mono (the chip already sets `font-mono`); a muted scope prefix goes in a `text-muted-foreground` span — e.g. `res:` for resource-scoped filters.
- Compose chips in a `flex flex-wrap items-center gap-2` row alongside the controls that add them.
- Chips represent state, not actions — clicking the body does nothing; only ✕ is interactive.
