import { afterEach } from "bun:test";
import { GlobalRegistrator } from "@happy-dom/global-registrator";

GlobalRegistrator.register();

// Imported only once the DOM globals exist. @testing-library's automatic cleanup hooks into
// jest/vitest globals, not bun:test, so without this every test inherits the previous test's
// markup and queries start matching several copies of the same element.
const { cleanup } = await import("@testing-library/react");

afterEach(cleanup);
