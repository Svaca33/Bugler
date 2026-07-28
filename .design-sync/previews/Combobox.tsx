import { Combobox, Label } from "bugler-frontend";

const OBSERVED_KEYS = [
  { value: "tenant.id", label: "tenant.id", group: "Attributes" },
  { value: "http.method", label: "http.method", group: "Attributes" },
  { value: "http.status_code", label: "http.status_code", group: "Attributes" },
  { value: "order.total", label: "order.total", group: "Attributes" },
  { value: "service.name", label: "service.name", group: "Resource" },
  { value: "service.version", label: "service.version", group: "Resource" },
  { value: "deployment.environment", label: "deployment.environment", group: "Resource" },
];

export const OpenWithKeys = () => (
  <div className="min-h-72 w-64">
    <Combobox
      autoFocus
      options={OBSERVED_KEYS}
      placeholder="Filter by key…"
      onSelect={() => {}}
    />
  </div>
);

export const Closed = () => (
  <div className="grid w-64 gap-2">
    <Label htmlFor="key">Attribute key</Label>
    <Combobox options={OBSERVED_KEYS} placeholder="Filter by key…" onSelect={() => {}} />
  </div>
);

export const Empty = () => (
  <div className="min-h-40 w-64">
    <Combobox
      autoFocus
      options={[]}
      placeholder="Filter by key…"
      emptyText="No keys observed."
      onSelect={() => {}}
    />
  </div>
);
