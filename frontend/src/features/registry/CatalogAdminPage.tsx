import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

/** Admin management of the telemetry topology: applications → instances → API keys. */
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
  const instanceCountOf = (applicationId: string) =>
    catalog.data?.applications.find(a => a.id === applicationId)?.instances.length ?? 0;

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
            const instanceCount = instanceCountOf(app.id);
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
                  {instanceCount} {instanceCount === 1 ? "instance" : "instances"}
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
        />
      ) : (
        <p className="p-6 text-sm text-[#8CA1B8]">
          Select an application to manage its instances and API keys.
        </p>
      )}
    </div>
  );
}

function TopologyDetail(props: { applicationId: string; applicationName: string }) {
  const queryClient = useQueryClient();
  const [issuedKey, setIssuedKey] = useState<{ instanceId: string; plaintext: string } | null>(null);

  const instances = useQuery({
    queryKey: ["admin", "instances", props.applicationId],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/applications/{applicationId}/instances", {
        params: { path: { applicationId: props.applicationId } },
      });
      if (error !== undefined) throw new Error("Failed to load instances");
      return data;
    },
  });

  const issueKey = useMutation({
    mutationFn: async (instanceId: string) => {
      const { data, error } = await api.POST("/api/admin/instances/{id}/keys", {
        params: { path: { id: instanceId } },
      });
      if (error !== undefined || data === undefined) throw new Error("Failed to issue key");
      return data;
    },
    onSuccess: (issued, instanceId) => {
      setIssuedKey({ instanceId, plaintext: issued.plaintext });
      queryClient.invalidateQueries({ queryKey: ["admin", "keys", instanceId] });
    },
  });

  const list = instances.data ?? [];

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex items-center gap-3">
        <h2 className="text-[17px] font-semibold tracking-[-0.3px]">{props.applicationName}</h2>
        <span className="rounded-[5px] border border-[#1E344C] px-[7px] py-[3px] font-mono text-[11px] text-[#7D93AA]">
          {props.applicationId}
        </span>
        <span className="ml-auto text-[12.5px] text-[#8CA1B8]">
          {list.length} {list.length === 1 ? "instance" : "instances"}
        </span>
      </div>

      {list.map(instance => (
        <InstanceCard
          key={instance.id}
          instance={instance}
          issuedPlaintext={issuedKey?.instanceId === instance.id ? issuedKey.plaintext : null}
          onIssue={() => issueKey.mutate(instance.id)}
          onIssuedSaved={() => setIssuedKey(null)}
        />
      ))}

      <AddInstanceForm applicationId={props.applicationId} />
    </div>
  );
}

function InstanceCard(props: {
  instance: { id: string; name: string; retentionDays?: number | string | null };
  issuedPlaintext: string | null;
  onIssue: () => void;
  onIssuedSaved: () => void;
}) {
  const queryClient = useQueryClient();
  const keys = useQuery({
    queryKey: ["admin", "keys", props.instance.id],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/instances/{id}/keys", {
        params: { path: { id: props.instance.id } },
      });
      if (error !== undefined) throw new Error("Failed to load keys");
      return data;
    },
  });

  const revoke = useMutation({
    mutationFn: async (keyId: string) => {
      await api.DELETE("/api/admin/keys/{id}", { params: { path: { id: keyId } } });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "keys", props.instance.id] }),
  });

  const activeKeys = (keys.data ?? []).filter(k => k.revokedAt == null);
  const retention = props.instance.retentionDays;

  return (
    <div className="flex flex-col gap-3 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <div className="flex items-center gap-2.5">
        <span className="font-mono text-[13px] font-medium text-foreground">{props.instance.name}</span>
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
      </div>

      {props.issuedPlaintext !== null && (
        <div className="flex flex-col gap-2 rounded-[9px] border border-[rgba(233,164,60,0.55)] bg-[rgba(233,164,60,0.10)] p-3.5">
          <p className="text-[12.5px] font-semibold text-[#F6E3C4]">
            New key for {props.instance.name}{" "}
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
          No API key yet — this instance cannot send telemetry until you issue one.
        </p>
      )}
    </div>
  );
}

function AddInstanceForm(props: { applicationId: string }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [retention, setRetention] = useState("");

  const createInstance = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/admin/instances", {
        body: {
          applicationId: props.applicationId,
          name,
          retentionDays: retention === "" ? null : Number(retention),
        },
      });
      if (error !== undefined || data === undefined) throw new Error("Failed to create instance");
      return data;
    },
    onSuccess: () => {
      setName("");
      setRetention("");
      queryClient.invalidateQueries({ queryKey: ["admin", "instances", props.applicationId] });
      queryClient.invalidateQueries({ queryKey: ["catalog"] });
    },
  });

  return (
    <form
      className="flex flex-col gap-[11px] rounded-[11px] border border-dashed border-input p-4"
      onSubmit={event => {
        event.preventDefault();
        if (name.trim()) createInstance.mutate();
      }}
    >
      <span className={CAPTION}>ADD INSTANCE</span>
      <div className="flex flex-wrap items-end gap-2">
        <div className="grid gap-1.5">
          <Label htmlFor="new-instance-name">Name (client)</Label>
          <Input
            id="new-instance-name"
            className="w-[232px]"
            placeholder="e.g. eu-west-1a"
            value={name}
            onChange={event => setName(event.target.value)}
          />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="new-instance-retention">Retention (days)</Label>
          <Input
            id="new-instance-retention"
            className="w-[176px]"
            type="number"
            min={1}
            placeholder="30"
            value={retention}
            onChange={event => setRetention(event.target.value)}
          />
        </div>
        <Button type="submit" size="sm" disabled={createInstance.isPending}>
          Add instance
        </Button>
      </div>
      <p className="text-[11.5px] text-[#7D93AA]">Leave retention empty to follow the server default.</p>
    </form>
  );
}
