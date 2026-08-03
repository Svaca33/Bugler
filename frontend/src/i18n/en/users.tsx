import type { UsersMessages } from "../sections/users";

export const users: UsersMessages = {
  title: "Users",
  accountCount: count => `${count} ${count === 1 ? "account" : "accounts"}`,
  deactivatedCount: count => `${count} deactivated`,
  filterPlaceholder: "Filter by e-mail…",
  noMatches: "No accounts match the filter.",

  header: {
    email: "E-MAIL",
    role: "ROLE",
    status: "STATUS",
    actions: "ACTIONS",
  },

  role: {
    admin: "Admin",
    member: "Member",
  },

  status: {
    active: "Active",
    activeYou: "Active · you",
    deactivated: "Deactivated",
  },

  deactivatedGrantsNote: "grants retained, no access while deactivated",
  adminScopeNote: "every application — admins are never scoped",
  mayRead: (email, application) => `${email} may read ${application}`,

  actions: {
    resetLink: "Reset link",
    deactivate: "Deactivate",
    reactivate: "Reactivate",
    delete: "Delete",
  },

  create: {
    caption: "CREATE USER",
    grantsHint: "Grants are assigned in the matrix above once the account exists.",
    emailLabel: "E-mail",
    emailPlaceholder: "name@company.com",
    passwordLabel: "Password (min 8 characters)",
    serverAdministrator: "Server administrator",
    submit: "Create user",
  },

  resetTicket: {
    title: "Reset link for this user",
    description: email => (
      <>
        Hand {email} this link yourself. It works once, stops working in an hour, and replaces any
        link they were sent before. Setting a password through it signs the account out everywhere.
      </>
    ),
    issuing: "Issuing…",
    close: "Close",
    copyLink: "Copy link",
    copied: "Copied",
  },

  deletion: {
    title: "Delete this user?",
    description: email => (
      <>
        {email} loses their account and every application grant it holds, and their e-mail becomes
        free for a new account. This cannot be undone — to only take their access away, deactivate
        them instead.
      </>
    ),
    failed: "Deletion failed — nothing was removed.",
    cancel: "Cancel",
    confirm: "Delete",
  },

  errors: {
    loadFailed: "Failed to load users",
    createFailed: "Failed to create user (password must have at least 8 characters).",
    ticketNotIssued: "No link was issued — the account may be deactivated.",
    deleteFailed: "Failed to delete user",
  },
};
