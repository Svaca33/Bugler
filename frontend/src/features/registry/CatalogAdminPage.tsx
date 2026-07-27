import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";

/** Admin management of the telemetry topology: applications → instances → API keys. */
export function CatalogAdminPage() {
  const queryClient = useQueryClient();
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

  return (
    <div className="grid gap-4 lg:grid-cols-[280px_1fr]">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Applications</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-2">
          {apps.map(app => (
            <Button
              key={app.id}
              variant={app.id === selectedApp ? "default" : "outline"}
              size="sm"
              className="justify-start"
              onClick={() => setSelectedApp(app.id)}
            >
              {app.name}
            </Button>
          ))}
          <form
            className="mt-2 flex gap-2"
            onSubmit={event => {
              event.preventDefault();
              if (newAppName.trim()) createApplication.mutate(newAppName.trim());
            }}
          >
            <Input
              placeholder="New application…"
              value={newAppName}
              onChange={event => setNewAppName(event.target.value)}
            />
            <Button type="submit" size="sm" disabled={createApplication.isPending}>
              Add
            </Button>
          </form>
        </CardContent>
      </Card>

      {selected !== null ? (
        <InstancesPanel applicationId={selected.id} applicationName={selected.name} />
      ) : (
        <p className="p-6 text-sm text-muted-foreground">
          Select an application to manage its instances and API keys.
        </p>
      )}
    </div>
  );
}

function InstancesPanel(props: { applicationId: string; applicationName: string }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [retention, setRetention] = useState("");
  const [issuedKey, setIssuedKey] = useState<string | null>(null);

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

  const issueKey = useMutation({
    mutationFn: async (instanceId: string) => {
      const { data, error } = await api.POST("/api/admin/instances/{id}/keys", {
        params: { path: { id: instanceId } },
      });
      if (error !== undefined || data === undefined) throw new Error("Failed to issue key");
      return data;
    },
    onSuccess: issued => setIssuedKey(issued.plaintext),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Instances of {props.applicationName}</CardTitle>
      </CardHeader>
      <CardContent className="grid gap-4">
        {issuedKey !== null && (
          <div className="rounded-md border border-amber-500/50 bg-amber-500/10 p-3 text-sm">
            <p className="mb-2 font-medium">
              API key issued — copy it now, it will never be shown again:
            </p>
            <code data-testid="issued-key" className="block break-all rounded bg-muted p-2 font-mono text-xs">
              {issuedKey}
            </code>
            <div className="mt-2 flex gap-2">
              <Button size="sm" variant="secondary" onClick={() => navigator.clipboard.writeText(issuedKey)}>
                Copy
              </Button>
              <Button size="sm" variant="outline" onClick={() => setIssuedKey(null)}>
                Dismiss
              </Button>
            </div>
          </div>
        )}

        <table className="w-full text-sm">
          <thead className="text-left text-muted-foreground">
            <tr>
              <th className="py-1 font-medium">Instance</th>
              <th className="py-1 font-medium">Retention</th>
              <th className="py-1 font-medium text-right">API keys</th>
            </tr>
          </thead>
          <tbody>
            {(instances.data ?? []).map(instance => (
              <InstanceRow
                key={instance.id}
                instance={instance}
                onIssue={() => issueKey.mutate(instance.id)}
              />
            ))}
          </tbody>
        </table>

        <form
          className="flex flex-wrap items-center gap-2"
          onSubmit={event => {
            event.preventDefault();
            if (name.trim()) createInstance.mutate();
          }}
        >
          <Input
            className="w-52"
            placeholder="New instance (client)…"
            value={name}
            onChange={event => setName(event.target.value)}
          />
          <Input
            className="w-40"
            type="number"
            min={1}
            placeholder="Retention (days)"
            value={retention}
            onChange={event => setRetention(event.target.value)}
          />
          <Button type="submit" size="sm" disabled={createInstance.isPending}>
            Add instance
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function InstanceRow(props: {
  instance: { id: string; name: string; retentionDays?: number | string | null };
  onIssue: () => void;
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

  return (
    <tr className="border-t">
      <td className="py-1.5">{props.instance.name}</td>
      <td className="py-1.5 text-muted-foreground">
        {props.instance.retentionDays != null ? `${props.instance.retentionDays} days` : "server default"}
      </td>
      <td className="py-1.5">
        <div className="flex items-center justify-end gap-2">
          {activeKeys.map(key => (
            <Button key={key.id} variant="outline" size="sm" onClick={() => revoke.mutate(key.id)}>
              Revoke key
            </Button>
          ))}
          <Button size="sm" variant="secondary" onClick={props.onIssue}>
            Issue key
          </Button>
        </div>
      </td>
    </tr>
  );
}
