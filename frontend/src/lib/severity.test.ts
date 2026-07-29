import { expect, test } from "bun:test";

import { severityLabel } from "./severity";

test("maps OTel severity numbers onto labels", () => {
  expect(severityLabel(0)).toBe("UNSET");
  expect(severityLabel(1)).toBe("TRACE");
  expect(severityLabel(8)).toBe("DEBUG");
  expect(severityLabel(9)).toBe("INFO");
  expect(severityLabel(13)).toBe("WARN");
  expect(severityLabel(17)).toBe("ERROR");
  expect(severityLabel(24)).toBe("FATAL");
});
