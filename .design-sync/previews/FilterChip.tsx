import { FilterChip } from "bugler-frontend";

export const Default = () => (
  <FilterChip onRemove={() => {}}>tenant.id = acme</FilterChip>
);

export const ResourceScope = () => (
  <FilterChip onRemove={() => {}}>
    <span className="text-muted-foreground">res:&thinsp;</span>service.name = checkout-service
  </FilterChip>
);

export const WithoutRemove = () => <FilterChip>order.total = 42</FilterChip>;

export const FilterBarRow = () => (
  <div className="flex flex-wrap items-center gap-2">
    <FilterChip onRemove={() => {}}>tenant.id = acme</FilterChip>
    <FilterChip onRemove={() => {}}>http.method = GET</FilterChip>
    <FilterChip onRemove={() => {}}>
      <span className="text-muted-foreground">res:&thinsp;</span>deployment.environment = production
    </FilterChip>
  </div>
);

export const LongValueTruncates = () => (
  <div className="w-56">
    <FilterChip onRemove={() => {}} className="max-w-full">
      exception.message = Payment declined: insufficient funds on card ending 6411
    </FilterChip>
  </div>
);
