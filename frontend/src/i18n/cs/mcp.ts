import type { McpMessages } from "../sections/mcp";

export const mcp: McpMessages = {
  delegation: "Strojová delegace",

  page: {
    title: "Strojové delegace",
    subtitle:
      "Nechte nástroj na svém počítači číst telemetrii vaším jménem. Strojová delegace nikdy " +
      "nevidí víc než vy, nic nezapisuje a v okamžiku, kdy ji odvoláte, přestane fungovat. " +
      "Aplikaci a platnost si nese od vydání a změnit je nelze — potřebujete-li jiné, tuhle " +
      "odvolejte a vydejte novou.",
    doorClosed:
      "Tento Bugler neotevírá strojové dveře. Správce je může otevřít ve Správě → Server.",
    addressMissing:
      "Dveře jsou otevřené, ale nikdo serveru neřekl, na jaké adrese odpovídá zvenčí. Nastaví ji " +
      "správce ve Správě → Server; do té doby vám příkaz níže nelze sestavit.",
  },

  list: {
    nameColumn: "Název",
    scopeColumn: "Čte",
    scopeEverything: "Vše, co smíte číst vy",
    createdColumn: "Vydána",
    expiresColumn: "Vyprší",
    lastUsedColumn: "Naposledy použita",
    neverUsed: "Nikdy",
    expiringSoon: days =>
      days === 0 ? "Vyprší dnes" : `Za ${days} ${days === 1 ? "den" : days < 5 ? "dny" : "dní"}`,
    empty:
      "Žádné strojové delegace. Vydejte si jednu a nechte nástroj číst telemetrii vaším jménem.",
    revoke: "Odvolat",
    revokeConfirm: name =>
      `Odvolat „${name}“? Cokoli drží její tajemství, okamžitě přestane číst. Zpět to nejde.`,
  },

  issue: {
    title: "Vydat strojovou delegaci",
    description: "Tajemství se ukáže jednou, tady, a nikdy víc.",
    nameLabel: "Název",
    namePlaceholder: "Pracovní notebook",
    applicationLabel: "Zúžit na",
    applicationAll: "Vše, co smíte číst vy",
    lifetimeLabel: "Platnost",
    lifetimeOption: days => `${days} dní`,
    submit: "Vydat",
    submitting: "Vydávám…",
  },

  issued: {
    title: "Zkopírujte to teď",
    description:
      "Tohle je jediná chvíle, kdy vám Bugler tajemství může ukázat. Zavřete to a je pryč — " +
      "pokud ho ztratíte, vydejte si novou strojovou delegaci.",
    commandLabel: "Zapojení nástroje",
    commandHint:
      "Spusťte tam, kde váš nástroj čte konfiguraci. Claude Code i Cursor umí HTTP MCP server " +
      "s hlavičkou.",
    copy: "Kopírovat",
    copied: "Zkopírováno",
    done: "Hotovo",
  },

  settings: {
    title: "MCP",
    description:
      "Jestli tenhle server pouští nástroje číst telemetrii přes MCP a na jaké adrese. Dokud to " +
      "nezapnete, je vypnuto. Delegace každého člověka pak stejně vidí jen to, co vidí on.",
    openedLabel: "Otevřít strojové dveře",
    openedHint:
      "Port je navázaný tak jako tak; tohle rozhoduje, jestli se na něm vůbec odpovídá. " +
      "Nesměrovat ten port zvenčí je druhá, silnější odpověď.",
    addressLabel: "Adresa zvenčí",
    addressHint:
      "Jen host a port — cestu si Bugler doplní sám. Vypíše se do příkazu, který si lidé kopírují, " +
      "a o ničem jiném nerozhoduje: ani o TLS, ani o cookies, ani o odkazech v mailu.",
    addressPlaceholder: "https://bugler.example.com",
    save: "Uložit",
    saving: "Ukládám…",
    saved: "Uloženo",
    reset: "Použít konfiguraci",
    fromConfiguration: "Z konfigurace",
    fromStored: "Uloženo zde",
  },

  held: {
    title: "Vydané strojové delegace",
    description:
      "Každá delegace, která na tomto serveru žije. Dveře jste otevřeli vy; tohle jimi prošlo.",
    holderColumn: "Držitel",
    empty: "Nikdo nedrží delegaci.",
    revoke: "Odvolat",
    revokeConfirm: (name, holder) =>
      `Odvolat „${name}“, kterou drží ${holder}? Cokoli drží její tajemství, okamžitě přestane číst.`,
  },
};
