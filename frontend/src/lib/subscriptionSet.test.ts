import { expect, test } from "bun:test";

import {
  isServiceSubscribed,
  toggleApplication,
  toggleService,
  type SubscriptionSet,
} from "./subscriptionSet";

const empty: SubscriptionSet = { applicationIds: [], serviceIds: [] };

test("subscribing an application swallows its services' direct subscriptions", () => {
  const withService = toggleService(empty, "svc-1");
  const withApp = toggleApplication(withService, "app-1", ["svc-1", "svc-2"]);

  expect(withApp.applicationIds).toEqual(["app-1"]);
  expect(withApp.serviceIds).toEqual([]);
});

test("unsubscribing an application does not resurrect swallowed services", () => {
  const subscribed = toggleApplication(empty, "app-1", ["svc-1"]);
  const unsubscribed = toggleApplication(subscribed, "app-1", ["svc-1"]);

  expect(unsubscribed).toEqual(empty);
});

test("a service under a subscribed application counts as subscribed", () => {
  const set = toggleApplication(empty, "app-1", []);

  expect(isServiceSubscribed(set, "app-1", "svc-new")).toBe(true);
  expect(isServiceSubscribed(set, "app-2", "svc-other")).toBe(false);
});

test("toggling a service flips only that service", () => {
  const on = toggleService(empty, "svc-1");
  expect(isServiceSubscribed(on, "app-1", "svc-1")).toBe(true);

  const off = toggleService(on, "svc-1");
  expect(isServiceSubscribed(off, "app-1", "svc-1")).toBe(false);
});
