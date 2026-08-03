import type { UsersMessages } from "../sections/users";
import { plural } from "../plural";

export const users: UsersMessages = {
  title: "Uživatelé",
  accountCount: count =>
    plural("cs", count, {
      one: `${count} účet`,
      few: `${count} účty`,
      other: `${count} účtů`,
    }),
  deactivatedCount: count =>
    plural("cs", count, {
      one: `${count} deaktivovaný`,
      few: `${count} deaktivované`,
      other: `${count} deaktivovaných`,
    }),
  filterPlaceholder: "Filtrovat podle e-mailu…",
  noMatches: "Filtru neodpovídá žádný účet.",

  header: {
    email: "E-MAIL",
    role: "ROLE",
    status: "STAV",
    actions: "AKCE",
  },

  role: {
    admin: "Správce",
    member: "Člen",
  },

  status: {
    active: "Aktivní",
    activeYou: "Aktivní · vy",
    deactivated: "Deaktivován",
  },

  deactivatedGrantsNote: "oprávnění zůstávají, po dobu deaktivace bez přístupu",
  adminScopeNote: "všechny aplikace — správci nejsou nikdy omezováni",
  mayRead: (email, application) => `${email} smí číst ${application}`,

  actions: {
    resetLink: "Odkaz pro obnovení",
    deactivate: "Deaktivovat",
    reactivate: "Reaktivovat",
    delete: "Smazat",
  },

  create: {
    caption: "VYTVOŘIT UŽIVATELE",
    grantsHint: "Oprávnění se přidělují v matici výše, jakmile účet existuje.",
    emailLabel: "E-mail",
    emailPlaceholder: "name@company.com",
    passwordLabel: "Heslo (min. 8 znaků)",
    serverAdministrator: "Správce serveru",
    submit: "Vytvořit uživatele",
  },

  resetTicket: {
    title: "Odkaz pro obnovení hesla tohoto uživatele",
    description: email => (
      <>
        Tento odkaz předejte uživateli {email} sami. Funguje jen jednou, za hodinu přestane platit
        a nahrazuje jakýkoli odkaz zaslaný dříve. Nastavením hesla přes něj se účet všude odhlásí.
      </>
    ),
    issuing: "Vystavování…",
    close: "Zavřít",
    copyLink: "Kopírovat odkaz",
    copied: "Zkopírováno",
  },

  deletion: {
    title: "Smazat tohoto uživatele?",
    description: email => (
      <>
        {email} přijde o účet i o všechna oprávnění k aplikacím, která účet drží, a jeho e-mail se
        uvolní pro nový účet. Tuto akci nelze vrátit — chcete-li pouze odebrat přístup, účet místo
        toho deaktivujte.
      </>
    ),
    failed: "Smazání se nezdařilo — nic nebylo odstraněno.",
    cancel: "Zrušit",
    confirm: "Smazat",
  },

  errors: {
    loadFailed: "Nepodařilo se načíst uživatele",
    createFailed: "Nepodařilo se vytvořit uživatele (heslo musí mít alespoň 8 znaků).",
    ticketNotIssued: "Žádný odkaz nebyl vystaven — účet může být deaktivovaný.",
    deleteFailed: "Nepodařilo se smazat uživatele",
  },
};
