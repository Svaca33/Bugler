// Compiles the design-sync stylesheet: the app's real Tailwind v4 CSS with
// extra source scans (frontend/src + .design-sync/previews) so component
// classes, preview compositions, and app-used glue utilities all generate.
// Output: frontend/.ds-css/entry.css (gitignored; consumed as cfg.cssEntry).
import tailwind from "bun-plugin-tailwind";
import { rm } from "node:fs/promises";
import path from "node:path";

const frontendDir = path.dirname(import.meta.dir);
const outdir = path.join(frontendDir, ".ds-css");
await rm(outdir, { recursive: true, force: true });

const result = await Bun.build({
  entrypoints: [path.join(import.meta.dir, "entry.css")],
  outdir,
  plugins: [tailwind],
  target: "browser",
});

if (!result.success) {
  for (const log of result.logs) console.error(String(log));
  process.exit(1);
}
for (const output of result.outputs) {
  console.log(`${path.relative(frontendDir, output.path)}  ${(output.size / 1024).toFixed(1)} KB`);
}
