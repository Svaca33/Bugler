---
category: Layout
---

The right-hand detail chrome every explorer page shares: one resizable panel with a title row, a close button, and a width the reader chose once and keeps everywhere. Reach for it whenever a list opens a record beside itself; reach for bare `ResizablePanel` only when the split is not the list-and-detail arrangement.

## Usage

- It must sit as a **direct child of a `ResizablePanelGroup`, after the list panel** — it renders its own `ResizableHandle` next to its own `ResizablePanel`, so the divider comes with it. A Fragment between them is fine; a wrapper `div` is not:

```tsx
<ResizablePanelGroup className="h-full">
  <ResizablePanel minSize="420px" className="flex h-full min-w-0 flex-col">
    …list…
  </ResizablePanel>
  {selected !== null && (
    <DetailPanel title={<span>Log record</span>} onClose={() => select(undefined)}>
      …detail sections…
    </DetailPanel>
  )}
</ResizablePanelGroup>
```

- Render it conditionally, as above: closing it returns the list to full width because the panel and its divider leave the group together.
- `title` takes any node — pages put a heading, a state badge, a duration. The close button is part of the chrome; `onClose` is where the selection is cleared.
- **The width is remembered across pages by design** (one stored pixel width, shared by Logs, Traces and Alerts): the question it answers is "how much room do I want for a detail", which is not a per-page question. Double-clicking the divider returns to the shipped default width.
- Children stack in a scrolling column with an 18px gap; sections bring their own captions. Values, ids and timestamps inside follow the mono rule (`font-mono`).
- **Its chrome is tuned for the dark theme** — the navy console the app pins itself to. Use it under a `class="dark"` root; on the light theme its foreground text sits dark-on-navy.
