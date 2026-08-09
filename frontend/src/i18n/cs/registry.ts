import { plural } from "../plural";
import type { RegistryMessages } from "../sections/registry";

/** "1 den / 2 dny / 5 dní" — the day count with its noun, everywhere retention names days. */
const dny = (count: number): string =>
  `${count} ${plural("cs", count, { one: "den", few: "dny", other: "dní" })}`;

export const registry: RegistryMessages = {
  cancel: "Zrušit",
  delete: "Smazat",

  catalog: {
    applicationsCaption: "APLIKACE",
    serviceCount: count =>
      `${count} ${plural("cs", count, { one: "služba", few: "služby", other: "služeb" })}`,
    addApplicationLabel: "Přidat aplikaci",
    applicationNamePlaceholder: "např. billing-api",
    addButton: "Přidat",
    selectApplicationPrompt: "Vyberte aplikaci pro správu jejích služeb a API klíčů.",
    deleteApplication: "Smazat aplikaci",
    deleteApplicationTitle: name => `Smazat aplikaci „${name}“?`,
    deleteApplicationConsequence: serviceCount =>
      plural("cs", serviceCount, {
        one: "Tím se smaže 1 služba, její API klíče a každý log i span, který kdy odeslala.",
        few: `Tím se smažou ${serviceCount} služby, jejich API klíče a každý log i span, který kdy odeslaly.`,
        other: `Tím se smaže ${serviceCount} služeb, jejich API klíče a každý log i span, který kdy odeslaly.`,
      }),
    deleteServiceTitle: label => `Smazat službu „${label}“?`,
    deleteServiceConsequence:
      "Tím se smažou její API klíče a každý log i span, který kdy odeslala. Traces sdílené "
      + "s jinými službami si ponechají zbývající spany.",
    servicesCaption: (logDays, traceDays) =>
      `SLUŽBY · výchozí ${dny(logDays)} logů a ${dny(traceDays)} traces`,
    addServiceCaption: "PŘIDAT SLUŽBU",
    namespaceLabel: "Namespace (nasazení)",
    namespacePlaceholder: "např. demo",
    environmentLabel: "Prostředí",
    environmentPlaceholder: "např. prod",
    serviceNameLabel: "Název služby",
    serviceNamePlaceholder: "např. backend",
    addServiceButton: "Přidat službu",
    addServiceHelp: defaults =>
      `Jeden proces, jedna registrace: backend a mobilní klient téhož nasazení jsou dvě služby s vlastními klíči. Ponechte kteroukoli retenci prázdnou a platí výchozí hodnota serveru${
        defaults === null ? "" : ` — ${dny(defaults.logDays)} logů, ${dny(defaults.traceDays)} traces`
      }.`,
  },

  keys: {
    issueButton: "Vydat klíč",
    newKeyFor: label => `Nový klíč pro ${label}`,
    shownOnce: "— zobrazí se jen jednou, zkopírujte si ho hned",
    copyButton: "Kopírovat",
    savedItButton: "Uloženo",
    activeKeysCaption: count => `AKTIVNÍ KLÍČE · ${count}`,
    revokeButton: "Odvolat",
    noKeyYet: "Zatím žádný API klíč — dokud nějaký nevydáte, služba nemůže posílat telemetrii.",
  },

  alertingCard: {
    caption: (sensitivity, quietWindowMinutes) =>
      `VÝSTRAHY · výchozí ${sensitivity} · tiché okno ${quietWindowMinutes} min`,
    sensitivityLabel: "Citlivost",
    sensitivity: {
      Off: "Vypnuto",
      Errors: "Chyby",
      ErrorsAndWarnings: "Chyby + varování",
    },
    defaultOption: label => `Výchozí (${label})`,
    inheritOption: label => `Zděděno (${label})`,
    quietWindowLabel: "Tiché okno (min)",
    quietWindowHelp:
      "Jak epizoda končí: jakmile služba po tento počet minut nezaloguje nic, co odpovídá "
      + "citlivosti, epizoda se uzavře a odejde zpráva, že je vše v pořádku. Každý nový "
      + "odpovídající log odpočet restartuje. Ponechte prázdné pro výchozí hodnotu.",
    explainer:
      "Vypnuto okamžitě a tiše uzavře otevřené epizody. Komu chodí e-maily, je volbou každého "
      + "v Epizody → Odběry; webhook posílá každou epizodu této aplikace do jednoho prostoru "
      + "Google Chat.",
    webhookLabel: "Google Chat webhook",
    webhookSet: domain => `nastaveno · ${domain}`,
    replaceButton: "Nahradit",
    removeButton: "Odstranit",
    saveButton: "Uložit",
    saveFailed: "Nastavení výstrah se nepodařilo uložit",
    webhookInvalid: "Webhook musí být absolutní https URL.",
    overrideSaveFailed: "Nastavení výstrah služby se nepodařilo uložit",
    logsWatch: "LOGY",
    healthCheckWatch: "HEALTH CHECK",
    healthCheckUrlLabel: "URL",
    healthCheckAnswered: "odpověděl",
    healthCheckNoAnswer: "bez odpovědi",
    healthCheckHelpBeforeCode:
      "Prázdné znamená, že se nikdo neptá. Cokoli jiného než 2xx — včetně přesměrování — se "
      + "počítá jako výpadek a tři selhání po sobě otevřou epizodu.",
    healthCheckHelpAfterCode: "zde znamená uvnitř kontejneru samotného Bugleru, ne váš počítač.",
  },

  aiCard: {
    caption: "AI",
    consentLabel: "Telemetrie této aplikace smí být ukázána AI providerovi",
    whatLeaves:
      "Když se otevře epizoda, Bugler pošle otevírací log včetně atributů (i stack trace), "
      + "posledních ~25 těl logů služby před ním a její poslední verzi release AI providerovi "
      + "nastavenému na záložce Server — aby napsal krátký výklad, co se nejspíš děje. Vypnuto, "
      + "dokud to zde někdo nezapne; odvolat lze kdykoli.",
    serverAiOffNote:
      "Server nemá AI nastavenou, takže tak jako tak nic neodchází — souhlas jen čeká.",
    saveFailed: "Souhlas se nepodařilo uložit.",
  },

  retention: {
    logs: {
      label: "Retence logů (dny)",
      name: "retenci logů",
      subject: "Logy",
    },
    traces: {
      label: "Retence traces (dny)",
      name: "retenci traces",
      subject: "Spany",
    },
    shortenTitle: (name, days) => `Zkrátit ${name} na ${dny(days)}?`,
    followsDefault: days => `Tato služba se bude řídit výchozí hodnotou serveru — ${dny(days)}. `,
    purgeConsequence: (subject, days) =>
      `${subject} starší než ${dny(days)} budou při příštím běhu mazání trvale smazány. Tuto akci nelze vzít zpět.`,
    saveFailedUnchanged: "Uložení se nezdařilo — retence zůstává beze změny.",
    shortenButton: "Zkrátit retenci",
    saveFailed: "Retenci se nepodařilo uložit.",
  },

  deleteDialog: {
    cannotBeUndone: "Tuto akci nelze vzít zpět.",
    typeBeforePhrase: "Napište",
    typeAfterPhrase: "pro potvrzení",
    failed: "Smazání se nezdařilo — nic nebylo odstraněno.",
  },
};
