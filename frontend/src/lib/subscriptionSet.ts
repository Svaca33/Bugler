/**
 * The caller's Subscriptions as the API exchanges them: two id sets, replaced wholesale on every
 * change. An application subscription covers all its services, present and future, so a checked
 * application makes its services' own checkboxes redundant.
 */
export interface SubscriptionSet {
  applicationIds: string[];
  serviceIds: string[];
}

export function isApplicationSubscribed(set: SubscriptionSet, applicationId: string): boolean {
  return set.applicationIds.includes(applicationId);
}

/** True for a direct service subscription and for one covered by its application's. */
export function isServiceSubscribed(
  set: SubscriptionSet, applicationId: string, serviceId: string): boolean {
  return isApplicationSubscribed(set, applicationId) || set.serviceIds.includes(serviceId);
}

/**
 * Subscribing an application swallows its services' direct subscriptions — they would resurface
 * as surprises after unsubscribing the application. Unsubscribing takes only the application
 * itself away.
 */
export function toggleApplication(
  set: SubscriptionSet, applicationId: string, serviceIdsOfApplication: string[]): SubscriptionSet {
  if (isApplicationSubscribed(set, applicationId)) {
    return {
      applicationIds: set.applicationIds.filter(id => id !== applicationId),
      serviceIds: set.serviceIds,
    };
  }

  return {
    applicationIds: [...set.applicationIds, applicationId],
    serviceIds: set.serviceIds.filter(id => !serviceIdsOfApplication.includes(id)),
  };
}

export function toggleService(set: SubscriptionSet, serviceId: string): SubscriptionSet {
  return set.serviceIds.includes(serviceId)
    ? { ...set, serviceIds: set.serviceIds.filter(id => id !== serviceId) }
    : { ...set, serviceIds: [...set.serviceIds, serviceId] };
}
