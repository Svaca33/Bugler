import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { serviceLabel } from "@/lib/serviceLabel";

import {
  isApplicationSubscribed,
  isServiceSubscribed,
  toggleApplication,
  toggleService,
  type SubscriptionSet,
} from "./subscriptionSet";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

/**
 * The viewer's own Subscriptions: an application checkbox covers all its services, present and
 * future; a service checkbox watches just that one. Every click sends the next full picture.
 */
export function SubscriptionsPanel() {
  const queryClient = useQueryClient();
  const catalog = useCatalog();

  const subscriptions = useQuery({
    queryKey: ["alerts", "subscriptions"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/subscriptions");
      if (error !== undefined) throw new Error("Failed to load subscriptions");
      return data;
    },
  });

  const update = useMutation({
    mutationFn: async (next: SubscriptionSet) => {
      const { error } = await api.PUT("/api/alerting/subscriptions", {
        body: { applicationIds: next.applicationIds, serviceIds: next.serviceIds },
      });
      if (error !== undefined) throw new Error("Failed to save subscriptions");
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["alerts", "subscriptions"] }),
  });

  const applications = catalog.data?.applications ?? [];
  const set: SubscriptionSet = {
    applicationIds: subscriptions.data?.applicationIds ?? [],
    serviceIds: subscriptions.data?.serviceIds ?? [],
  };
  const ready = subscriptions.data !== undefined;

  return (
    <div className="flex max-w-[560px] flex-col gap-5 overflow-auto px-6 py-5">
      <p className="text-[12.5px] text-[#8CA1B8]">
        Mail lands in your account inbox when a subscribed service opens or resolves an episode.
        Subscribing an application covers all its services — including ones registered later.
      </p>
      {update.error !== null && (
        <p className="text-[12.5px] text-[#F0685A]">{update.error.message}</p>
      )}

      {applications.map(application => {
        const applicationSubscribed = isApplicationSubscribed(set, application.id);
        return (
          <div key={application.id} className="flex flex-col gap-2.5">
            <div className="flex items-center gap-2.5">
              <Checkbox
                id={`subscribe-app-${application.id}`}
                checked={applicationSubscribed}
                disabled={!ready || update.isPending}
                onCheckedChange={() => update.mutate(toggleApplication(
                  set, application.id, application.services.map(service => service.id)))}
              />
              <Label htmlFor={`subscribe-app-${application.id}`} className="font-mono text-[13px]">
                {application.name}
              </Label>
              <span className={CAPTION}>ALL SERVICES, PRESENT AND FUTURE</span>
            </div>
            <div className="flex flex-col gap-1.5 pl-7">
              {application.services.map(service => (
                <div key={service.id} className="flex items-center gap-2.5">
                  <Checkbox
                    id={`subscribe-service-${service.id}`}
                    checked={isServiceSubscribed(set, application.id, service.id)}
                    disabled={!ready || update.isPending || applicationSubscribed}
                    onCheckedChange={() => update.mutate(toggleService(set, service.id))}
                  />
                  <Label
                    htmlFor={`subscribe-service-${service.id}`}
                    className={`font-normal font-mono text-[12px] ${
                      applicationSubscribed ? "text-[#6E86A0]" : "text-[#A9BDD1]"
                    }`}
                  >
                    {serviceLabel(service)}
                  </Label>
                </div>
              ))}
            </div>
          </div>
        );
      })}

      {applications.length === 0 && !catalog.isPending && (
        <p className="text-[#8CA1B8]">
          Nothing to subscribe to — no application is visible to you.
        </p>
      )}
    </div>
  );
}
