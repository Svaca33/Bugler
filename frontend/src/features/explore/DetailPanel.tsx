import { useEffect, useRef, type ReactNode } from "react";
import { usePanelRef } from "react-resizable-panels";

import { ResizableHandle, ResizablePanel } from "@/components/ui/resizable";

import {
  DEFAULT_DETAIL_WIDTH,
  MIN_DETAIL_WIDTH,
  readDetailWidth,
  writeDetailWidth,
} from "./detailWidth";

/** Long enough that a drag writes once when it settles, short enough to survive a quick tab-away. */
const SAVE_DELAY_MS = 200;

/**
 * The right-hand detail on Logs and Traces: one chrome, one remembered width, two sets of contents.
 *
 * It renders its own divider next to its own panel, so a page opens the whole thing with a single
 * conditional node. Both must land as direct DOM children of the panel group — a Fragment is fine,
 * it creates no element of its own.
 */
export function DetailPanel(props: { title: ReactNode; onClose: () => void; children: ReactNode }) {
  const panel = usePanelRef();
  // Read once, at mount. Re-reading per render would fight a drag that is still in progress.
  const initialWidth = useRef(readDetailWidth()).current;
  const pendingSave = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => () => clearTimeout(pendingSave.current), []);

  return (
    <>
      <ResizableHandle
        withHandle
        // The built-in double-click resets to `defaultSize` — which here is the *remembered* width,
        // so it would put the panel back exactly where it already is. Ours returns to the shipped
        // default, which is the only width a reader can ask for by name.
        disableDoubleClick
        onDoubleClick={() => panel.current?.resize(DEFAULT_DETAIL_WIDTH)}
      />
      <ResizablePanel
        id="detail"
        panelRef={panel}
        defaultSize={initialWidth}
        minSize={`${MIN_DETAIL_WIDTH}px`}
        // Pixels, not a fraction: resizing the window must not drift the width the reader chose.
        // The list panel keeps the default relative behaviour and absorbs the difference.
        groupResizeBehavior="preserve-pixel-size"
        onResize={(size, _id, previous) => {
          if (previous === undefined) return; // Mount reports a size too; that is not a resize.
          clearTimeout(pendingSave.current);
          pendingSave.current = setTimeout(() => writeDetailWidth(size.inPixels), SAVE_DELAY_MS);
        }}
        className="flex"
      >
        {/* No `border-l` any more — the divider is the border now. */}
        <aside className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto bg-[#0B1826] px-5 py-4">
          <div className="flex items-center gap-2">
            {props.title}
            <button
              type="button"
              aria-label="Close"
              className="ml-auto rounded-[5px] px-1.5 py-0.5 text-[13px] text-muted-foreground hover:bg-[#16283C] hover:text-foreground"
              onClick={props.onClose}
            >
              ✕
            </button>
          </div>
          {props.children}
        </aside>
      </ResizablePanel>
    </>
  );
}
