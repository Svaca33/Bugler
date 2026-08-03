import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { useCatalog, useCurrentUser } from "@/api/queries";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useT } from "@/i18n";
import { serviceLabel } from "@/lib/serviceLabel";
import {
  isApplicationSubscribed,
  isServiceSubscribed,
  toggleApplication,
  toggleService,
  type SubscriptionSet,
} from "@/lib/subscriptionSet";

/**
 * The viewer's own Subscriptions: an application checkbox covers all its services, present and
 * future; a service checkbox watches just that one. Every click sends the next full picture.
 */
export function SubscriptionsPanel() {
  const t = useT();
  const queryClient = useQueryClient();
  const catalog = useCatalog();
  const currentUser = useCurrentUser();
  const [filter, setFilter] = useState("");

  const subscriptions = useQuery({
    queryKey: ["alerts", "subscriptions"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/subscriptions");
      if (error !== undefined) throw new Error(t.alerting.errors.loadSubscriptions);
      return data;
    },
  });

  // Which visible services do not alert at all right now — subscribing to silence is a trap,
  // so their checkboxes are closed rather than merely warned about.
  const sensitivity = useQuery({
    queryKey: ["alerts", "sensitivity"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/sensitivity");
      if (error !== undefined) throw new Error(t.alerting.errors.loadSensitivity);
      return new Map(data.items.map(item => [item.serviceId, item.off]));
    },
  });

  const update = useMutation({
    mutationFn: async (next: SubscriptionSet) => {
      const { error } = await api.PUT("/api/alerting/subscriptions", {
        body: { applicationIds: next.applicationIds, serviceIds: next.serviceIds },
      });
      if (error !== undefined) throw new Error(t.alerting.errors.saveSubscriptions);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["alerts", "subscriptions"] }),
  });

  const applications = catalog.data?.applications ?? [];
  const set: SubscriptionSet = {
    applicationIds: subscriptions.data?.applicationIds ?? [],
    serviceIds: subscriptions.data?.serviceIds ?? [],
  };
  const ready = subscriptions.data !== undefined;

  // What the summary announces: every service the reader is mailed about, however covered.
  const coveredServices = new Set<string>();
  const coveredApplications = new Set<string>();
  for (const application of applications) {
    for (const service of application.services) {
      if (isServiceSubscribed(set, application.id, service.id)) {
        coveredServices.add(service.id);
        coveredApplications.add(application.id);
      }
    }
    if (isApplicationSubscribed(set, application.id)) coveredApplications.add(application.id);
  }

  const needle = filter.trim().toLowerCase();
  const shown = applications
    .map(application => {
      const applicationMatches =
        needle === "" || application.name.toLowerCase().includes(needle);
      const services = applicationMatches
        ? application.services
        : application.services.filter(s => serviceLabel(s).toLowerCase().includes(needle));
      return { application, services };
    })
    .filter(entry => entry.services.length > 0 || needle === "");

  const email = currentUser.data?.email ?? t.alerting.subscriptions.accountInbox;

  return (
    <div className="h-full overflow-auto">
      <div className="mx-auto flex max-w-[760px] flex-col gap-4 px-6 py-[22px]">
        <div className="rounded-[11px] border border-[#1E344C] bg-card p-[14px_16px]">
          <p className="text-[13.5px] font-semibold">
            {t.alerting.subscriptions.summary(coveredServices.size, coveredApplications.size)}
          </p>
          <p className="mt-1 text-[12.5px] text-[#8CA1B8]">
            {t.alerting.subscriptions.body(email)}
          </p>
        </div>

        {update.error !== null && (
          <p className="text-[12.5px] text-[#F0685A]">{update.error.message}</p>
        )}

        <Input
          placeholder={t.alerting.subscriptions.filterPlaceholder}
          className="max-w-sm"
          value={filter}
          onChange={event => setFilter(event.target.value)}
        />

        {shown.map(({ application, services }) => {
          const applicationSubscribed = isApplicationSubscribed(set, application.id);
          const ownCount = application.services
            .filter(s => set.serviceIds.includes(s.id)).length;
          return (
            <div
              key={application.id}
              className="rounded-[11px] border border-[#1E344C] bg-card p-[13px_16px]"
            >
              <div className="flex items-center gap-2.5 pb-[9px]">
                <Checkbox
                  id={`subscribe-app-${application.id}`}
                  checked={applicationSubscribed}
                  disabled={!ready || update.isPending}
                  onCheckedChange={() => update.mutate(toggleApplication(
                    set, application.id, application.services.map(service => service.id)))}
                />
                <Label
                  htmlFor={`subscribe-app-${application.id}`}
                  className="font-mono text-[13px]"
                >
                  {application.name}
                </Label>
                {applicationSubscribed ? (
                  <span className="rounded-[5px] bg-[#16283C] px-1.5 font-mono text-[10px] tracking-[0.08em] text-[#A9BDD1]">
                    {t.alerting.subscriptions.allServicesBadge}
                  </span>
                ) : ownCount > 0 && (
                  <span className="font-mono text-[10px] tracking-[0.08em] text-[#5F7590]">
                    {t.alerting.subscriptions.serviceCount(ownCount, application.services.length)}
                  </span>
                )}
              </div>

              {services.map(service => {
                const off = sensitivity.data?.get(service.id) === true;
                const subscribedItself = set.serviceIds.includes(service.id);
                return (
                  <div
                    key={service.id}
                    className="flex items-center gap-2.5 border-t border-[#101F31] py-[5px] pl-[26px]"
                  >
                    <Checkbox
                      id={`subscribe-service-${service.id}`}
                      checked={isServiceSubscribed(set, application.id, service.id)}
                      // A silenced service cannot be newly subscribed; one already subscribed
                      // stays uncheckable-off only in the one direction that traps nobody.
                      disabled={!ready || update.isPending || applicationSubscribed
                        || (off && !subscribedItself)}
                      onCheckedChange={() => update.mutate(toggleService(set, service.id))}
                    />
                    <Label
                      htmlFor={`subscribe-service-${service.id}`}
                      className={`font-mono text-[12px] font-normal ${
                        applicationSubscribed || off ? "text-[#6E86A0]" : "text-[#A9BDD1]"
                      }`}
                    >
                      {serviceLabel(service)}
                    </Label>
                    {off && (
                      <span className="ml-auto rounded border border-[#22394F] px-1.5 font-mono text-[11px] whitespace-nowrap text-[#6E86A0]">
                        {t.alerting.subscriptions.sensitivityOff}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          );
        })}

        {applications.length === 0 && !catalog.isPending && (
          <p className="text-[#8CA1B8]">{t.alerting.subscriptions.nothingVisible}</p>
        )}

        <p className="text-[11.5px] text-[#6E86A0]">{t.alerting.subscriptions.footer}</p>
      </div>
    </div>
  );
}
