/** The registered facets that identify a Service, in the shape every API response carries them. */
export interface ServiceFacets {
  namespace: string;
  environment: string;
  name: string;
}

/** `demo/prod · backend` — derived from the registered facets so it can never drift from them. */
export function serviceLabel(service: ServiceFacets): string {
  return `${service.namespace}/${service.environment} · ${service.name}`;
}
