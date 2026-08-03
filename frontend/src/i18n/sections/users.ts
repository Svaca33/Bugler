import type { ReactNode } from "react";

/** The admin's user roster: accounts, roles, grants, reset tickets and deletion. */
export interface UsersMessages {
  title: string;
  /** "3 accounts" — the count phrase beside the heading. */
  accountCount(count: number): string;
  /** "2 deactivated" — appended after the count when any account is off. */
  deactivatedCount(count: number): string;
  filterPlaceholder: string;
  noMatches: string;

  /** Uppercase mono captions over the roster columns. */
  header: {
    email: string;
    role: string;
    status: string;
    actions: string;
  };

  role: {
    admin: string;
    member: string;
  };

  status: {
    active: string;
    activeYou: string;
    deactivated: string;
  };

  /** Spans the grant columns of a deactivated row. */
  deactivatedGrantsNote: string;
  /** Spans the grant columns of an admin row. */
  adminScopeNote: string;
  /** aria-label of a grant checkbox. */
  mayRead(email: string, application: string): string;

  actions: {
    resetLink: string;
    deactivate: string;
    reactivate: string;
    delete: string;
  };

  create: {
    caption: string;
    grantsHint: string;
    emailLabel: string;
    /** An example address, not a sentence — languages keep it as an address. */
    emailPlaceholder: string;
    passwordLabel: string;
    serverAdministrator: string;
    submit: string;
  };

  resetTicket: {
    title: string;
    /** The addressee arrives already wrapped in its `code` styling. */
    description(email: ReactNode): ReactNode;
    issuing: string;
    close: string;
    copyLink: string;
    copied: string;
  };

  deletion: {
    title: string;
    /** The addressee arrives already wrapped in its `code` styling. */
    description(email: ReactNode): ReactNode;
    failed: string;
    cancel: string;
    confirm: string;
  };

  errors: {
    loadFailed: string;
    createFailed: string;
    ticketNotIssued: string;
    deleteFailed: string;
  };
}
