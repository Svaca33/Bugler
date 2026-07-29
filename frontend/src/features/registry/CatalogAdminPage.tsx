import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { serviceLabel } from "@/lib/serviceLabel";

import { ApplicationAlertingCard } from "./ApplicationAlertingCard";
import { DeleteConfirmation } from "./DeleteConfirmation";
import { serviceConfirmationPhrase } from "./deletionConfirmation";
import { ServiceAlertingOverride } from "./ServiceAlertingOverride";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

/** Admin management of the telemetry topology: applications → services → API keys. */
export function CatalogAdminPage() {
  const queryClient = useQueryClient();
  const catalog = useCatalog();
  const [selectedApp, setSelectedApp] = useState<string | null>(null);
  const [newAppName, setNewAppName] = useState("");

  const applications = useQuery({
    queryKey: ["admin", "applications"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/applications");
      if (error !== undefined) throw new Error("Failed to load applications");
      return data;
    },
  });

  const createApplication = useMutation({
    mutationFn: async (name: string) => {
      const { data, error } = await api.POST("/api/admin/applications", { body: { name } });
      if (error !== undefined || data === undefined) throw new Error("Failed to create application");
      return data;
    },
    onSuccess: created => {
      setNewAppName("");
      setSelectedApp(created.id);
      queryClient.invalidateQueries({ queryKey: ["admin", "applications"] });
      queryClient.invalidateQueries({ queryKey: ["catalog"] });
    },
  });

  const apps = applications.data ?? [];
  const selected = apps.find(a => a.id === selectedApp) ?? null;
  const serviceCountOf = (applicationId: string) =>
    catalog.data?.applications.find(a => a.id === applicationId)?.services.length ?? 0;

  return (
    <div className="flex h-full min-h-0">
      <aside className="flex w-[296px] shrink-0 flex-col border-r border-[#17293D] bg-[#0B1826]">
        <div className="flex items-center px-[18px] pt-3.5 pb-2.5">
          <span className={CAPTION.replace("0.12em", "0.14em")}>APPLICATIONS</span>
          <span className="ml-auto font-mono text-[11px] text-[#6E86A0]">{apps.length}</span>
        </div>

        <div className="flex min-h-0 flex-1 flex-col gap-0.5 overflow-auto px-2.5">
          {apps.map(app => {
            const isSelected = app.id === selectedApp;
            const serviceCount = serviceCountOf(app.id);
            return (
              <button
                key={app.id}
                type="button"
                onClick={() => setSelectedApp(app.id)}
                className={`flex cursor-pointer flex-col items-start gap-[3px] rounded-lg px-[11px] py-[9px] text-left ${
                  isSelected
                    ? "bg-[rgba(233,164,60,0.11)] shadow-[inset_2px_0_0_#E9A43C]"
                    : "hover:bg-[#12253A]"
                }`}
              >
                <span
                  className={`font-mono text-[12.5px] ${isSelected ? "text-[#F6C170]" : "text-[#DCE8F3]"}`}
                >
                  {app.name}
                </span>
                <span className={`text-[11px] ${isSelected ? "text-[#8CA1B8]" : "text-[#7D93AA]"}`}>
                  {serviceCount} {serviceCount === 1 ? "service" : "services"}
                </span>
              </button>
            );
          })}
        </div>

        <form
          className="flex flex-col gap-2 border-t border-[#17293D] px-[18px] py-3.5"
          onSubmit={event => {
            event.preventDefault();
            if (newAppName.trim()) createApplication.mutate(newAppName.trim());
          }}
        >
          <Label htmlFor="new-application">Add application</Label>
          <div className="flex gap-2">
            <Input
              id="new-application"
              placeholder="e.g. billing-api"
              value={newAppName}
              onChange={event => setNewAppName(event.target.value)}
            />
            <Button type="submit" size="sm" disabled={createApplication.isPending}>
              Add
            </Button>
          </div>
        </form>
      </aside>

      {selected !== null ? (
        <TopologyDetail
          key={selected.id}
          applicationId={selected.id}
          applicationName={selected.name}
          onDeleted={() => setSelectedApp(null)}
        />
      ) : (
        <p className="p-6 text-sm text-[#8CA1B8]">
          Select an application to manage its services and API keys.
        </p>
      )}
    </div>
  );
}

function TopologyDetail(props: {
  applicationId: string;
  applicationName: string;
  onDeleted: () => void;
}) {
  const queryClient = useQueryClient();
  const [issuedKey, setIssuedKey] = useState<{ serviceId: string; plaintext: string } | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const services = useQuery({
    queryKey: ["admin", "services", props.applicationId],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/applications/{applicationId}/services", {
        params: { path: { applicationId: props.applicationId } },
      });
      if (error !== undefined) throw new Error("Failed to load services");
      return data;
    },
  });

  const issueKey = useMutation({
    mutationFn: async (serviceId: string) => {
      const { data, error } = await api.POST("/api/admin/services/{id}/keys", {
        params: { path: { id: serviceId } },
      });
      if (error !== undefined || data === undefined) throw new Error("Failed to issue key");
      return data;
    },
    onSuccess: (issued, serviceId) => {
      setIssuedKey({ serviceId, plaintext: issued.plaintext });
      queryClient.invalidateQueries({ queryKey: ["admin", "keys", serviceId] });
    },
  });

  const deleteApplication = useMutation({
    mutationFn: async () => {
      const { error } = await api.DELETE("/api/admin/applications/{id}", {
        params: { path: { id: props.applicationId } },
      });
      if (error !== undefined) throw new Error("Failed to delete application");
    },
    onSuccess: () => {
      setConfirmingDelete(false);
      queryClient.invalidateQueries({ queryKey: ["admin", "applications"] });
      queryClient.invalidateQueries({ queryKey: ["catalog"] });
      props.onDeleted();
    },
  });

  const list = services.data ?? [];

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex items-center gap-3">
        <h2 className="text-[17px] font-semibold tracking-[-0.3px]">{props.applicationName}</h2>
        <span className="rounded-[5px] border border-[#1E344C] px-[7px] py-[3px] font-mono text-[11px] text-[#7D93AA]">
          {props.applicationId}
        </span>
        <span className="ml-auto text-[12.5px] text-[#8CA1B8]">
          {list.length} {list.length === 1 ? "service" : "services"}
        </span>
        <button
          type="button"
          className="rounded-[5px] px-2 py-[3px] text-[11.5px] text-[#F0685A] hover:bg-[rgba(229,84,74,0.14)]"
          onClick={() => setConfirmingDelete(true)}
        >
          Delete application
        </button>
      </div>

      <DeleteConfirmation
        inputId={props.applicationId}
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete application "${props.applicationName}"?`}
        consequence={`This erases ${list.length} ${list.length === 1 ? "service" : "services"}, their API keys and every log and span they ever sent.`}
        phrase={props.applicationName}
        pending={deleteApplication.isPending}
        failed={deleteApplication.isError}
        onConfirm={() => deleteApplication.mutate()}
      />

      <ApplicationAlertingCard applicationId={props.applicationId} />

      {list.map(service => (
        <ServiceCard
          key={service.id}
          applicationId={props.applicationId}
          service={service}
          issuedPlaintext={issuedKey?.serviceId === service.id ? issuedKey.plaintext : null}
          onIssue={() => issueKey.mutate(service.id)}
          onIssuedSaved={() => setIssuedKey(null)}
        />
      ))}

      <AddServiceForm applicationId={props.applicationId} />
    </div>
  );
}

function ServiceCard(props: {
  applicationId: string;
  service: {
    id: string;
    namespace: string;
    environment: string;
    name: string;
    retentionDays?: number | string | null;
  };
  issuedPlaintext: string | null;
  onIssue: () => void;
  onIssuedSaved: () => void;
}) {
  const queryClient = useQueryClient();
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const keys = useQuery({
    queryKey: ["admin", "keys", props.service.id],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/services/{id}/keys", {
        params: { path: { id: props.service.id } },
      });
      if (error !== undefined) throw new Error("Failed to load keys");
      return data;
    },
  });

  const revoke = useMutation({
    mutationFn: async (keyId: string) => {
      await api.DELETE("/api/admin/keys/{id}", { params: { path: { id: keyId } } });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "keys", props.service.id] }),
  });

  const deleteService = useMutation({
    mutationFn: async () => {
      const { error } = await api.DELETE("/api/admin/services/{id}", {
        params: { path: { id: props.service.id } },
      });
      if (error !== undefined) throw new Error("Failed to delete service");
    },
    onSuccess: () => {
      setConfirmingDelete(false);
      queryClient.invalidateQueries({ queryKey: ["admin", "services", props.applicationId] });
      queryClient.invalidateQueries({ queryKey: ["catalog"] });
    },
  });

  const activeKeys = (keys.data ?? []).filter(k => k.revokedAt == null);
  const retention = props.service.retentionDays;
  const label = serviceLabel(props.service);

  return (
    <div className="flex flex-col gap-3 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <div className="flex items-center gap-2.5">
        <span className="font-mono text-[13px] font-medium text-foreground">{label}</span>
        <span
          className={`rounded-[5px] bg-[#16283C] px-[7px] py-0.5 font-mono text-[10.5px] ${
            retention != null ? "text-[#A9BDD1]" : "text-[#8CA1B8]"
          }`}
        >
          {retention != null ? `retention ${retention} d` : "server default"}
        </span>
        <Button size="sm" variant="secondary" className="ml-auto" onClick={props.onIssue}>
          Issue key
        </Button>
        <button
          type="button"
          className="rounded-[5px] px-2 py-[3px] text-[11.5px] text-[#F0685A] hover:bg-[rgba(229,84,74,0.14)]"
          onClick={() => setConfirmingDelete(true)}
        >
          Delete
        </button>
      </div>

      <DeleteConfirmation
        inputId={props.service.id}
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete service "${label}"?`}
        consequence="This erases its API keys and every log and span it ever sent. Traces it shared with other services keep their remaining spans."
        phrase={serviceConfirmationPhrase(props.service)}
        pending={deleteService.isPending}
        failed={deleteService.isError}
        onConfirm={() => deleteService.mutate()}
      />

      <ServiceAlertingOverride applicationId={props.applicationId} serviceId={props.service.id} />

      {props.issuedPlaintext !== null && (
        <div className="flex flex-col gap-2 rounded-[9px] border border-[rgba(233,164,60,0.55)] bg-[rgba(233,164,60,0.10)] p-3.5">
          <p className="text-[12.5px] font-semibold text-[#F6E3C4]">
            New key for {label}{" "}
            <span className="font-normal text-[#D9A45E]">— shown once, copy it now</span>
          </p>
          <code
            data-testid="issued-key"
            className="block break-all rounded-md border border-input bg-background p-2.5 font-mono text-xs text-[#F6E3C4]"
          >
            {props.issuedPlaintext}
          </code>
          <div className="flex gap-2">
            <Button
              size="sm"
              onClick={() => navigator.clipboard.writeText(props.issuedPlaintext ?? "")}
            >
              Copy
            </Button>
            <Button size="sm" variant="ghost" onClick={props.onIssuedSaved}>
              I saved it
            </Button>
          </div>
        </div>
      )}

      {activeKeys.length > 0 ? (
        <div className="flex flex-col gap-1.5">
          <span className={CAPTION}>ACTIVE KEYS · {activeKeys.length}</span>
          {activeKeys.map(key => (
            <div
              key={key.id}
              className="flex items-center gap-2.5 rounded-[7px] bg-sidebar px-2.5 py-2"
            >
              <span className="font-mono text-[11.5px] text-[#DCE8F3]">
                key_{key.id.replaceAll("-", "").slice(0, 8)}
              </span>
              <span className="text-[11.5px] text-[#7D93AA]">
                {new Date(key.createdAt).toLocaleDateString()}
              </span>
              <button
                type="button"
                className="ml-auto rounded-[5px] px-2 py-[3px] text-[11.5px] text-[#F0685A] hover:bg-[rgba(229,84,74,0.14)]"
                onClick={() => revoke.mutate(key.id)}
              >
                Revoke
              </button>
            </div>
          ))}
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-input p-3 text-xs text-[#8CA1B8]">
          No API key yet — this service cannot send telemetry until you issue one.
        </p>
      )}
    </div>
  );
}

function AddServiceForm(props: { applicationId: string }) {
  const queryClient = useQueryClient();
  const [namespace, setNamespace] = useState("");
  const [environment, setEnvironment] = useState("");
  const [name, setName] = useState("");
  const [retention, setRetention] = useState("");

  const createService = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/admin/services", {
        body: {
          applicationId: props.applicationId,
          namespace,
          environment,
          name,
          retentionDays: retention === "" ? null : Number(retention),
        },
      });
      if (error !== undefined || data === undefined) throw new Error("Failed to create service");
      return data;
    },
    onSuccess: () => {
      setNamespace("");
      setEnvironment("");
      setName("");
      setRetention("");
      queryClient.invalidateQueries({ queryKey: ["admin", "services", props.applicationId] });
      queryClient.invalidateQueries({ queryKey: ["catalog"] });
    },
  });

  const complete = namespace.trim() !== "" && environment.trim() !== "" && name.trim() !== "";

  return (
    <form
      className="flex flex-col gap-[11px] rounded-[11px] border border-dashed border-input p-4"
      onSubmit={event => {
        event.preventDefault();
        if (complete) createService.mutate();
      }}
    >
      <span className={CAPTION}>ADD SERVICE</span>
      <div className="flex flex-wrap items-end gap-2">
        <div className="grid gap-1.5">
          <Label htmlFor="new-service-namespace">Namespace (deployment)</Label>
          <Input
            id="new-service-namespace"
            className="w-[176px]"
            placeholder="e.g. demo"
            value={namespace}
            onChange={event => setNamespace(event.target.value)}
          />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="new-service-environment">Environment</Label>
          <Input
            id="new-service-environment"
            className="w-[136px]"
            placeholder="e.g. prod"
            value={environment}
            onChange={event => setEnvironment(event.target.value)}
          />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="new-service-name">Service name</Label>
          <Input
            id="new-service-name"
            className="w-[176px]"
            placeholder="e.g. backend"
            value={name}
            onChange={event => setName(event.target.value)}
          />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="new-service-retention">Retention (days)</Label>
          <Input
            id="new-service-retention"
            className="w-[136px]"
            type="number"
            min={1}
            placeholder="30"
            value={retention}
            onChange={event => setRetention(event.target.value)}
          />
        </div>
        <Button type="submit" size="sm" disabled={!complete || createService.isPending}>
          Add service
        </Button>
      </div>
      <p className="text-[11.5px] text-[#7D93AA]">
        One process, one registration: a backend and a mobile client of the same deployment are two
        services with their own keys. Leave retention empty to follow the server default.
      </p>
    </form>
  );
}
