---
category: Layout
---

Two or more panels the reader can re-proportion by dragging the divider between them (`react-resizable-panels`). Bugler uses it for the one arrangement it names: a list on the left, the record you opened on the right. Reach for it when the split is the reader's to choose; use plain flex when the widths are the design's decision.

## Usage

- `ResizablePanelGroup` is the container and it *must* be the direct DOM parent of every `ResizablePanel` and `ResizableHandle`. A Fragment between them is fine — it contributes no element — but a wrapper `div` is not:

```tsx
<ResizablePanelGroup className="h-full">
  <ResizablePanel minSize="420px" className="flex h-full flex-col">
    …list…
  </ResizablePanel>
  <ResizableHandle withHandle />
  <ResizablePanel defaultSize={384} minSize="320px">
    …detail…
  </ResizablePanel>
</ResizablePanelGroup>
```

- **Sizes read their units from their type**: a number is pixels (`defaultSize={384}` is 384px), a bare string is a *percentage* (`defaultSize="384"` is 384%, which is not what anyone means). Write `"320px"` or `"50%"` and the ambiguity goes away.
- Give every panel a `minSize` in px. It is the only thing standing between a drag and a column of unreadable stubs, and the neighbouring panel's minimum is what caps this one — there is no need to also invent a `maxSize`.
- `withHandle` puts a visible grip on the divider. Prefer it: without one, nothing on screen says the split can move. The bare divider is for places where the affordance is already obvious.
- **The height must come from the parent.** The group writes `height: 100%` on itself inline, so a height utility *on the group* is ignored. Put it inside something with a definite height — a `flex-1` cell of a full-height flex column, or a sized box. In an auto-height parent a horizontal group survives on its content, but a vertical one collapses to nothing.
- `className` on a panel lands on an inner element, not the flex item, so layout utilities inside it (`flex`, `flex-col`, `h-full`) behave normally and cannot fight the group's own sizing.
- Keyboard is built in: the divider is focusable, arrows resize it, Home/End go to the extremes, F6 cycles between dividers. Do not add key handlers.
- Double-clicking the divider restores a panel to its `defaultSize`. Pass `disableDoubleClick` and your own `onDoubleClick` when the default size is itself remembered, or the gesture resets to where the reader already is.
- Set `groupResizeBehavior="preserve-pixel-size"` on a side panel so resizing the window does not drift the width the reader chose; the other panel absorbs the difference. At least one panel in the group must keep the default relative behaviour.
