import { expect, test } from "bun:test";
import { render, screen } from "@testing-library/react";
import {
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from "@tanstack/react-router";

import { NavTab } from "./-nav-tab";

/** The real bar in miniature: two tabs over routes that carry Explore's filters in the URL. */
async function renderBarAt(url: string) {
  const rootRoute = createRootRoute({
    component: () => (
      <nav>
        <NavTab to="/" label="Logs" />
        <NavTab to="/traces" label="Traces" />
      </nav>
    ),
  });
  const routeTree = rootRoute.addChildren([
    createRoute({ getParentRoute: () => rootRoute, path: "/", validateSearch: passThrough }),
    createRoute({ getParentRoute: () => rootRoute, path: "/traces", validateSearch: passThrough }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  render(<RouterProvider router={router as never} />);
  await screen.findByRole("link", { name: "Logs" });

  return (label: string) => screen.getByRole("link", { name: label }).classList.contains("active");
}

function passThrough(search: Record<string, unknown>) {
  return search;
}

test("a tab stays marked while its page is filtered", async () => {
  const isActive = await renderBarAt("/?range=PT1H");

  expect(isActive("Logs")).toBe(true);
  expect(isActive("Traces")).toBe(false);
});

test("only the tab of the page being viewed is marked", async () => {
  const isActive = await renderBarAt("/traces?range=PT1H&errorsOnly=true");

  expect(isActive("Traces")).toBe(true);
  expect(isActive("Logs")).toBe(false);
});
