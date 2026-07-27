/**
 * Frontend architecture rules — the counterpart of Bugler.ArchitectureTests.
 * Feature folders will mirror the bounded contexts (ingestion is backend-only;
 * expect features/explore, features/registry, features/access) and must not
 * import each other's internals.
 */
module.exports = {
  forbidden: [
    {
      name: "no-circular",
      severity: "error",
      comment: "Circular dependencies make modules impossible to reason about in isolation.",
      from: {},
      to: { circular: true },
    },
    {
      name: "features-are-isolated",
      severity: "error",
      comment:
        "A feature may not reach into another feature; shared code belongs in components/ or lib/.",
      from: { path: "^src/features/([^/]+)/" },
      to: { path: "^src/features/([^/]+)/", pathNot: "^src/features/$1/" },
    },
    {
      name: "ui-is-leaf",
      severity: "error",
      comment: "Reusable UI components must not depend on features or routes.",
      from: { path: "^src/components/" },
      to: { path: "^src/(features|routes)/" },
    },
  ],
  options: {
    doNotFollow: { path: "node_modules" },
    exclude: { path: "\\.test\\.tsx?$|src/routeTree\\.gen\\.ts$" },
    tsPreCompilationDeps: true,
    tsConfig: { fileName: "tsconfig.json" },
  },
};
