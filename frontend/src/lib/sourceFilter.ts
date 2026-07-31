import type { Catalog } from "@/api/client";
import { serviceLabel } from "@/lib/serviceLabel";

/**
 * A Source Filter: which Services a query may touch, addressed by registered facets
 * rather than by picking one registration. Every facet left open means "all".
 */
export interface SourceFilters {
  applicationId?: string;
  namespace?: string;
  environment?: string;
  service?: string;
}

type CatalogApplication = Catalog["applications"][number];
type CatalogService = CatalogApplication["services"][number];

interface FlatService extends CatalogService {
  applicationId: string;
}

export type Facet = "namespace" | "environment" | "service";

const FACET_FIELD = {
  namespace: "namespace",
  environment: "environment",
  service: "name",
} as const;

function flatten(applications: readonly CatalogApplication[]): FlatService[] {
  return applications.flatMap(application =>
    application.services.map(service => ({ ...service, applicationId: application.id })),
  );
}

function matches(service: FlatService, filters: SourceFilters, ignore?: Facet): boolean {
  if (filters.applicationId !== undefined && service.applicationId !== filters.applicationId) return false;
  if (ignore !== "namespace" && filters.namespace !== undefined && service.namespace !== filters.namespace)
    return false;
  if (ignore !== "environment" && filters.environment !== undefined && service.environment !== filters.environment)
    return false;
  if (ignore !== "service" && filters.service !== undefined && service.name !== filters.service) return false;
  return true;
}

/**
 * The values a facet can still be set to without emptying the result — every other
 * facet stays applied, so picking "mobile" leaves only the deployments that run one.
 */
export function facetOptions(
  applications: readonly CatalogApplication[],
  filters: SourceFilters,
  facet: Facet,
): string[] {
  const field = FACET_FIELD[facet];
  const values = new Set<string>();
  for (const service of flatten(applications)) {
    if (matches(service, filters, facet)) values.add(service[field]);
  }

  const current = filters[facet];
  if (current !== undefined) values.add(current);
  return [...values].sort((a, b) => a.localeCompare(b));
}

/** Resolves the service id stamped on a Signal to its display label. */
export function serviceLabels(applications: readonly CatalogApplication[]): Map<string, string> {
  return new Map(flatten(applications).map(service => [service.id, serviceLabel(service)]));
}
