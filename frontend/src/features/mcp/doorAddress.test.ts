import { describe, expect, test } from "bun:test";

import { connectCommand, doorAddress } from "./useMachineDelegations";

/**
 * The path is Bugler's own fact and the address is the operator's. What these pin down is that the
 * command never depends on whoever filled the settings in having guessed the other half.
 */
describe("doorAddress", () => {
  test("adds the path the operator has no reason to know", () => {
    expect(doorAddress("http://localhost:8081")).toBe("http://localhost:8081/mcp");
    expect(doorAddress("https://bugler.example.com")).toBe("https://bugler.example.com/mcp");
  });

  test("leaves an address that already carries it alone", () => {
    expect(doorAddress("https://bugler.example.com/mcp")).toBe("https://bugler.example.com/mcp");
  });

  test("survives the trailing slashes and spaces people paste", () => {
    expect(doorAddress("  http://localhost:8081/  ")).toBe("http://localhost:8081/mcp");
    expect(doorAddress("https://bugler.example.com/mcp/")).toBe("https://bugler.example.com/mcp");
  });

  test("puts the secret inside the command rather than beside it", () => {
    expect(connectCommand("http://localhost:8081", "blgrd_secret")).toBe(
      'claude mcp add --transport http bugler http://localhost:8081/mcp ' +
        '--header "Authorization: Bearer blgrd_secret"',
    );
  });
});
