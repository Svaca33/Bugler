---
category: Overlays
---

Modal dialog (Radix Dialog). A compound component — always this skeleton:

```tsx
<Dialog open={open} onOpenChange={setOpen}>
  <DialogContent>
    <DialogHeader>
      <DialogTitle>Delete service?</DialogTitle>
      <DialogDescription>This cannot be undone.</DialogDescription>
    </DialogHeader>
    <DialogFooter>
      <Button variant="ghost" onClick={() => setOpen(false)}>Cancel</Button>
      <Button variant="destructive" onClick={confirm}>Delete</Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

## Parts

- `Dialog` — the stateful root: `open`/`onOpenChange` (controlled) or `defaultOpen`. No DOM of its own.
- `DialogTrigger` — optional; the element that opens it. Omit it when the dialog is opened from elsewhere (a row action, a menu item) and drive `open` yourself.
- `DialogContent` — the centered panel, portalled above a scrim, with a focus trap. `showCloseButton={false}` drops the corner ✕ when dismissal must go through an explicit button. Width is `sm:max-w-lg` by default — narrow it with `className="sm:max-w-md"`.
- `DialogHeader` / `DialogTitle` / `DialogDescription` — the titled block. `DialogTitle` is required for accessibility; `DialogDescription` is what Radix announces as the body.
- `DialogFooter` — the action row: stacked on mobile, right-aligned on `sm`. Put the cancelling action first so the confirming one lands rightmost.
- `DialogClose` — wraps any element that should dismiss without your own state.

## Judgment

- One dialog, one decision. Anything with several steps or its own navigation belongs on a page.
- Order the footer cancel-then-confirm; the confirming action carries the variant (`destructive` for deletions, `default` otherwise).
- Escape and a scrim click already dismiss it — never rely on the ✕ being the only way out.
- For an irreversible action, name what is lost in `DialogDescription` and gate the confirming button behind a typed confirmation (Bugler's catalog deletions type the application name or `namespace/environment/name`).
- Wrap the body in a `<form>` when the dialog collects input, so Enter submits and the confirming button can be `type="submit"`.
