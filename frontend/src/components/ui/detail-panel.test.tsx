import { afterEach, expect, test } from "bun:test";
import { render, screen } from "@testing-library/react";

import { ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";

import { DetailPanel } from "./detail-panel";
import { MIN_LIST_WIDTH, writeDetailWidth } from "@/lib/detailWidth";

function renderPanel() {
  return render(
    <ResizablePanelGroup>
      <ResizablePanel id="list" minSize={`${MIN_LIST_WIDTH}px`}>
        list
      </ResizablePanel>
      <DetailPanel title={<h2>Log record</h2>} onClose={() => {}}>
        <p>attributes</p>
      </DetailPanel>
    </ResizablePanelGroup>,
  );
}

afterEach(() => localStorage.clear());

test("the divider and the panel land as siblings inside the group", () => {
  // A Fragment is what lets `DetailPanel` return both; the library requires them to be direct
  // DOM children of the group element, and a Fragment contributes no element of its own.
  const { container } = renderPanel();
  const group = container.querySelector("[data-group]")!;

  const kinds = [...group.children].map(child =>
    child.hasAttribute("data-separator") ? "separator" : "panel",
  );
  expect(kinds).toEqual(["panel", "separator", "panel"]);
});

test("the divider is a real separator, reachable and announced", () => {
  renderPanel();

  const separator = screen.getByRole("separator");
  expect(separator.getAttribute("tabindex")).toBe("0");
  // "vertical" describes the separator's own axis: the line between side-by-side panels.
  expect(separator.getAttribute("aria-orientation")).toBe("vertical");
});

test("the page's header content and its close button share one header row", () => {
  renderPanel();

  const heading = screen.getByRole("heading", { name: "Log record" });
  const close = screen.getByRole("button", { name: "Close" });
  expect(heading.parentElement).toBe(close.parentElement);
  expect(screen.getByText("attributes")).toBeDefined();
});

/*
 * The restored width is deliberately NOT asserted here. The library expresses a panel's size as
 * `flex-grow`, and turning the stored pixels into that fraction needs a measured group width —
 * which happy-dom, having no layout engine, always reports as zero, so every panel comes out an
 * even split no matter what was stored. Asserting it would only measure the test environment.
 * What the width actually depends on is covered in `detailWidth.test.ts`; that the panel is the
 * one carrying it is covered below.
 */
test("the detail is the panel that carries the remembered width", () => {
  writeDetailWidth(700);
  const { container } = renderPanel();

  expect(container.querySelector('[data-panel][id="detail"]')).not.toBeNull();
});
