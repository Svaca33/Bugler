---
status: accepted
---

# React + TypeScript frontend instead of Blazor

The frontend is React + TypeScript (Vite, shadcn/ui, TanStack Router and Query) even though the team is .NET-centric and Blazor was the natural alternative. Bugler's UI is dominated by data-dense, custom visualization — virtualized log lists, trace waterfalls, URL-driven typed filters — where the React ecosystem is substantially stronger and Blazor would push us into JS interop anyway.

## Consequences

The build requires a Node toolchain and TypeScript skills alongside .NET. Filters live in the URL as typed search parameters (TanStack Router), so log views are shareable links. Frontend module boundaries mirror the bounded contexts and are enforced with dependency-cruiser, matching the backend's architecture tests.
