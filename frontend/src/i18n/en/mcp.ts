import type { McpMessages } from "../sections/mcp";

export const mcp: McpMessages = {
  delegation: "Machine delegation",

  page: {
    title: "Machine delegations",
    subtitle:
      "Let a tool on your machine read telemetry in your name. A machine delegation never sees " +
      "more than you do, writes nothing beyond the machine hand you may grant it, and stops " +
      "working the moment you revoke it. Its application, grade and lifetime are fixed at issue " +
      "and cannot be edited — if you need different ones, revoke this and issue another.",
    doorClosed:
      "This Bugler does not open a machine door. An administrator can open it under " +
      "Administration → Server.",
    addressMissing:
      "The door is open, but nobody has told this server the address it answers at from outside. " +
      "An administrator sets it under Administration → Server; until then the command below " +
      "cannot be written for you.",
  },

  list: {
    nameColumn: "Name",
    scopeColumn: "Reads",
    scopeEverything: "Everything you can read",
    createdColumn: "Issued",
    expiresColumn: "Expires",
    lastUsedColumn: "Last used",
    neverUsed: "Never",
    expiringSoon: days => (days === 0 ? "Expires today" : `In ${days} ${days === 1 ? "day" : "days"}`),
    empty: "No machine delegations. Issue one to let a tool read telemetry in your name.",
    revoke: "Revoke",
    revokeConfirm: name =>
      `Revoke "${name}"? Whatever holds its secret stops reading immediately. This cannot be undone.`,
  },

  issue: {
    title: "Issue a machine delegation",
    description: "The secret is shown once, here, and never again.",
    nameLabel: "Name",
    namePlaceholder: "Work laptop",
    applicationLabel: "Narrow to",
    applicationAll: "Everything you can read",
    lifetimeLabel: "Expires after",
    lifetimeOption: days => `${days} days`,
    submit: "Issue",
    submitting: "Issuing…",
  },

  grade: {
    column: "May do",
    label: "Grade",
    reading: "Read only",
    machineHand: "Read + machine hand",
    hint:
      "The machine hand lets an agent claim an episode, pin a note, propose Solved and resign. " +
      "Solved stays a human verdict either way.",
  },

  issued: {
    title: "Copy this now",
    description:
      "This is the only time Bugler can show the secret. Close this and it is gone — issue " +
      "another one if you lose it.",
    commandLabel: "Connect your tool",
    commandHint:
      "Run this where your tool reads its configuration. Claude Code and Cursor both take an " +
      "HTTP MCP server with a header.",
    copy: "Copy",
    copied: "Copied",
    done: "Done",
  },

  settings: {
    title: "MCP",
    description:
      "Whether this server lets tools read telemetry over MCP, and the address they reach it at. " +
      "Off until you turn it on. Each person's machine delegation still sees only what they see.",
    openedLabel: "Open the machine door",
    openedHint:
      "The port is bound either way; this decides whether anything is answered on it. Leaving " +
      "the port unrouted from outside is the second, stronger answer.",
    addressLabel: "Address from outside",
    addressHint:
      "Host and port only — Bugler adds its own path. Printed into the connect command people " +
      "copy, and it decides nothing else: not TLS, not cookies, not links in mail.",
    addressPlaceholder: "https://bugler.example.com",
    save: "Save",
    saving: "Saving…",
    saved: "Saved",
    reset: "Use configuration",
    fromConfiguration: "From configuration",
    fromStored: "Saved here",
  },

  held: {
    title: "Machine delegations in issue",
    description:
      "Every machine delegation live on this server. You opened the door; this is what went through it.",
    holderColumn: "Holder",
    empty: "Nobody holds a machine delegation.",
    revoke: "Revoke",
    revokeConfirm: (name, holder) =>
      `Revoke "${name}", held by ${holder}? Whatever holds its secret stops reading immediately.`,
  },
};
