import {
  Label,
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from "bugler-frontend";

export const Default = () => (
  <div className="grid w-56 gap-2">
    <Label htmlFor="environment">Environment</Label>
    <Select defaultValue="production">
      <SelectTrigger id="environment" className="w-full">
        <SelectValue placeholder="Choose environment" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="production">Production</SelectItem>
        <SelectItem value="staging">Staging</SelectItem>
        <SelectItem value="development">Development</SelectItem>
      </SelectContent>
    </Select>
  </div>
);

export const FilterBar = () => (
  <div className="flex flex-wrap items-center gap-2">
    <Select>
      <SelectTrigger className="w-44" size="sm">
        <SelectValue placeholder="All applications" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="__all__">All applications</SelectItem>
        <SelectItem value="checkout">checkout-service</SelectItem>
        <SelectItem value="billing">billing-service</SelectItem>
      </SelectContent>
    </Select>
    <Select defaultValue="error">
      <SelectTrigger className="w-44" size="sm">
        <SelectValue placeholder="All severities" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="error">Error and above</SelectItem>
        <SelectItem value="warning">Warning and above</SelectItem>
        <SelectItem value="info">Info and above</SelectItem>
      </SelectContent>
    </Select>
    <Select disabled>
      <SelectTrigger className="w-44" size="sm">
        <SelectValue placeholder="Time range" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="24h">Last 24 hours</SelectItem>
      </SelectContent>
    </Select>
  </div>
);

export const OpenWithGroups = () => (
  <div className="min-h-72">
    <Select defaultValue="checkout" defaultOpen>
      <SelectTrigger className="w-56">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectGroup>
          <SelectLabel>Services</SelectLabel>
          <SelectItem value="checkout">checkout-service</SelectItem>
          <SelectItem value="billing">billing-service</SelectItem>
          <SelectItem value="identity">identity-service</SelectItem>
        </SelectGroup>
        <SelectSeparator />
        <SelectGroup>
          <SelectLabel>Frontends</SelectLabel>
          <SelectItem value="web">web-app</SelectItem>
          <SelectItem value="mobile" disabled>
            mobile-app (no data)
          </SelectItem>
        </SelectGroup>
      </SelectContent>
    </Select>
  </div>
);
