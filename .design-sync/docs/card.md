---
category: Layout
---

The surface for grouping related content — dashboard panels, forms, detail sections. A compound component: compose the parts, never rebuild the paddings by hand.

## Composition

```tsx
<Card>
  <CardHeader>
    <CardTitle>Ingestion rate</CardTitle>
    <CardDescription>Events accepted in the last 24 hours</CardDescription>
    <CardAction>
      <Button variant="ghost" size="icon-sm" aria-label="Refresh"><RefreshCwIcon /></Button>
    </CardAction>
  </CardHeader>
  <CardContent>…main content…</CardContent>
  <CardFooter>
    <Button size="sm">View details</Button>
  </CardFooter>
</Card>
```

- `Card` — the bordered, rounded (rounded-xl) surface. Vertical padding is built in (py-6); horizontal padding comes from the parts (px-6). Width is the parent's job — put cards in a grid (`grid gap-4 md:grid-cols-2`) or give them `w-full`/`max-w-*`.
- `CardHeader` — holds `CardTitle` (semibold, one line) and optional `CardDescription` (muted). Add className `border-b` for a divided header — its bottom padding adjusts automatically.
- `CardAction` — optional; slots into the header's top-right corner (an icon button, a badge, a link). Must be a direct child of `CardHeader`.
- `CardContent` — the body. Bare content, tables, form fields.
- `CardFooter` — horizontal flex row for actions. Add className `border-t` for a divided footer.

## Judgment

- Header and footer are optional; `<Card><CardContent>…</CardContent></Card>` is a valid minimal panel.
- Don't nest cards. For subdivisions inside a card use spacing or a `border-t` separator.
- Stat tiles: CardDescription as the metric label above a large value in CardContent works well.
