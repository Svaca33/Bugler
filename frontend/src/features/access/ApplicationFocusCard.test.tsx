import { expect, test } from "bun:test";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";

import { ApplicationFocusCard } from "./ApplicationFocusCard";
import { en } from "@/i18n/en";

type Catalog = { applications: { id: string; name: string; services: [] }[] };
type Session = { focusedApplicationIds: string[] };

/**
 * The card over a primed cache: the two queries it reads are seeded rather than fetched, so this
 * is about what the card says in each state and never about the network.
 */
function renderCard(catalog: Catalog, session: Session) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(["catalog", "all"], catalog);
  queryClient.setQueryData(["auth", "me"], session);

  render(
    <QueryClientProvider client={queryClient}>
      <ApplicationFocusCard />
    </QueryClientProvider>,
  );
}

const eshop = { id: "11111111-1111-1111-1111-111111111111", name: "Eshop", services: [] as [] };
const crm = { id: "22222222-2222-2222-2222-222222222222", name: "CRM", services: [] as [] };

test("offers every application the reader may read, not only the watched ones", () => {
  renderCard({ applications: [eshop, crm] }, { focusedApplicationIds: [eshop.id] });

  expect(screen.getByLabelText("Eshop")).toBeTruthy();
  // The whole point of scope=all: an unwatched application must stay tickable back on.
  expect(screen.getByLabelText("CRM")).toBeTruthy();
});

test("says so when the reader is watching nothing", () => {
  renderCard({ applications: [eshop] }, { focusedApplicationIds: [] });

  expect(screen.getByText(en.access.focus.attendingToNothing)).toBeTruthy();
});

test("an empty server is a different emptiness and gets its own sentence", () => {
  renderCard({ applications: [] }, { focusedApplicationIds: [] });

  expect(screen.getByText(en.access.focus.nothingToAttendTo)).toBeTruthy();
  expect(screen.queryByText(en.access.focus.attendingToNothing)).toBeNull();
});

test("stays quiet while something is watched", () => {
  renderCard({ applications: [eshop] }, { focusedApplicationIds: [eshop.id] });

  expect(screen.queryByText(en.access.focus.attendingToNothing)).toBeNull();
});
