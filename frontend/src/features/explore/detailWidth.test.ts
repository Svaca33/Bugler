import { describe, expect, test } from "bun:test";

import {
  DEFAULT_DETAIL_WIDTH,
  MIN_DETAIL_WIDTH,
  readDetailWidth,
  writeDetailWidth,
} from "./detailWidth";

function fakeStorage(initial?: string) {
  const entries = new Map<string, string>();
  if (initial !== undefined) entries.set("bugler.explore.detail-width", initial);
  return {
    entries,
    getItem: (key: string) => entries.get(key) ?? null,
    setItem: (key: string, value: string) => {
      entries.set(key, value);
    },
  };
}

const refusing = {
  getItem() {
    throw new Error("storage is disabled");
  },
  setItem() {
    throw new Error("storage is disabled");
  },
};

describe("readDetailWidth", () => {
  test("a reader who has never dragged gets the width the panel has always had", () => {
    expect(readDetailWidth(fakeStorage())).toBe(DEFAULT_DETAIL_WIDTH);
  });

  test("a remembered width comes back", () => {
    expect(readDetailWidth(fakeStorage("640"))).toBe(640);
  });

  test("a width below the minimum is not honoured", () => {
    expect(readDetailWidth(fakeStorage(String(MIN_DETAIL_WIDTH - 1)))).toBe(DEFAULT_DETAIL_WIDTH);
  });

  test("junk in storage does not become a layout", () => {
    expect(readDetailWidth(fakeStorage("wide please"))).toBe(DEFAULT_DETAIL_WIDTH);
  });

  test("storage that refuses to be read is not an error path", () => {
    expect(readDetailWidth(refusing)).toBe(DEFAULT_DETAIL_WIDTH);
  });
});

describe("writeDetailWidth", () => {
  test("a dragged width is stored whole — subpixels are not a preference", () => {
    const storage = fakeStorage();
    writeDetailWidth(512.4, storage);

    expect(storage.entries.get("bugler.explore.detail-width")).toBe("512");
  });

  test("what is written is what comes back", () => {
    const storage = fakeStorage();
    writeDetailWidth(700, storage);

    expect(readDetailWidth(storage)).toBe(700);
  });

  test("storage that refuses to be written does not throw at the reader", () => {
    expect(() => writeDetailWidth(700, refusing)).not.toThrow();
  });
});
