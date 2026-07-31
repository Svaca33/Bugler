import type { Catalog } from "@/api/client";

type CatalogApplication = Catalog["applications"][number];

/** A Service resolved through the catalog: its application and the facets that label it. */
export interface KnownService {
  application: CatalogApplication;
  facets: CatalogApplication["services"][number];
}

/** The id → identity map every episode row and card resolves its Service through. */
export function indexServices(
  applications: readonly CatalogApplication[],
): Map<string, KnownService> {
  return new Map(applications.flatMap(application =>
    application.services.map(service =>
      [service.id, { application, facets: service }] as const)));
}
