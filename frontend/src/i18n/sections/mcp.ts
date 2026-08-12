/**
 * The machine door and the Machine Delegations that open it: a person's own, an Admin's sight of every
 * one, and the switch above them all. What the door itself answers to a tool is machine-facing and
 * lives in the server, in English (ADR 0024) — nothing of that is here.
 */
export interface McpMessages {
  /** The word for the thing, used wherever one is named. Not "token". */
  delegation: string;

  page: {
    title: string;
    subtitle: string;
    /** Shown in place of everything else while the Admin has not opened the door. */
    doorClosed: string;
    /** The door is open but nobody told the server its outside address. */
    addressMissing: string;
  };

  list: {
    nameColumn: string;
    scopeColumn: string;
    /** A Machine Delegation narrowed to nothing in particular follows its holder's whole scope. */
    scopeEverything: string;
    createdColumn: string;
    expiresColumn: string;
    lastUsedColumn: string;
    neverUsed: string;
    expiringSoon: (days: number) => string;
    empty: string;
    revoke: string;
    revokeConfirm: (name: string) => string;
  };

  issue: {
    title: string;
    description: string;
    nameLabel: string;
    namePlaceholder: string;
    applicationLabel: string;
    applicationAll: string;
    lifetimeLabel: string;
    lifetimeOption: (days: number) => string;
    submit: string;
    submitting: string;
  };

  /** The grade stamped in at issue: reading alone, or reading and the machine hand. */
  grade: {
    /** Column header in both lists — the holder's and the Admin's. */
    column: string;
    /** The issue form's label. */
    label: string;
    reading: string;
    machineHand: string;
    /** Under the select: what the machine hand is, and that Solved stays a human verdict. */
    hint: string;
  };

  issued: {
    title: string;
    /** Said once, plainly: this is the only time the Secret exists anywhere Bugler can show it. */
    description: string;
    commandLabel: string;
    commandHint: string;
    copy: string;
    copied: string;
    done: string;
  };

  settings: {
    title: string;
    description: string;
    openedLabel: string;
    openedHint: string;
    addressLabel: string;
    addressHint: string;
    addressPlaceholder: string;
    save: string;
    saving: string;
    saved: string;
    reset: string;
    /** Which of the two is answering, said the way the mail and AI cards say it. */
    fromConfiguration: string;
    fromStored: string;
  };

  held: {
    title: string;
    description: string;
    holderColumn: string;
    empty: string;
    /** The narrower answer to a lost laptop than deactivating the person who lost it. */
    revoke: string;
    revokeConfirm: (name: string, holder: string) => string;
  };
}
