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

  groupingCard: {
    caption: "CO JE TÁŽ POTÍŽ",
    ruleLabel: "Seskupovat podle",
    rule: {
      ThrowingCode: "Kódu, který chybu vyhodil",
      KindOfFailure: "Druhu selhání",
      WhatWasSaid: "Toho, co bylo řečeno",
    },
    attributeLabel: "Nebo podle tohoto atributu",
    attributePlaceholder: "acme.error_code",
    scopeCaption: "KAM AŽ JEDNA EPIZODA SAHÁ",
    byEnvironment: "Musí sedět prostředí",
    byNamespace: "Musí sedět namespace",
    byServiceName: "Musí sedět název služby",
    confirmTitle: "Přeskupit tuto aplikaci?",
    confirmIntro:
      "Měníte, co se tady počítá za tutéž potíž — buď z čeho se otisk destiluje, nebo kam až "
      + "jedna epizoda sahá.",
    warningCounting: "Zjišťuji, co tahle změna bude stát…",
    warning: (openEpisodes, capped) =>
      openEpisodes === 0
        ? "Uložením se druhy potíží této aplikace přerozdělí. Teď není nic otevřené, ale všechna "
          + "vyladěná tichá okna se zahodí. Vrátit to nejde."
        : `Uložení ztlumí ${capped ? "nejméně " : ""}${openEpisodes} `
          + `${openEpisodes === 1 ? "otevřenou epizodu" : openEpisodes < 5 ? "otevřené epizody" : "otevřených epizod"} `
          + "a zahodí všechna vyladěná tichá okna: jejich druhy potíží skončí v oddílu, který už "
          + "nikdo nenahlásí. Převzetí a strojní držení na nich padnou s nimi. Vrátit to nejde.",
    confirmButton: "Přeskupit",
    done: (mutedEpisodes, droppedQuietWindows) =>
      `Přeskupeno: ztlumeno ${mutedEpisodes} `
      + `${mutedEpisodes === 1 ? "epizoda" : mutedEpisodes < 5 ? "epizody" : "epizod"}, `
      + `zahozeno ${droppedQuietWindows} `
      + `${droppedQuietWindows === 1 ? "vyladěné tiché okno" : droppedQuietWindows < 5 ? "vyladěná tichá okna" : "vyladěných tichých oken"}.`,
    explainer:
      "Epizoda sahá napříč službami, takže obě nastavení patří aplikaci a žádná služba je "
      + "nepřebíjí. Kde kód, který chybu vyhodil, přečíst nejde — neznámý runtime, žádný stack — "
      + "se seskupení samo zhrubne a epizoda to řekne.",
    saveFailed: "Nastavení seskupování se nepodařilo uložit",
    countFailed: "Otevřené epizody se nepodařilo spočítat",
  },

  groupingHelp: {
    title: "Jak funguje seskupování",
    description:
      "Rozhodují dvě nastavení: z čeho se druh potíží destiluje a kam až jedna epizoda sahá. Obě "
      + "patří aplikaci — epizoda jde napříč službami, takže se konce musí shodnout.",
    ladderLabel: "ŽEBŘÍK — Z ČEHO SE OTISK DESTILUJE",
    finer: "ROZLIŠUJE NEJVÍC",
    coarser: "ROZLIŠUJE NEJMÍŇ",
    rungAboveTheRule: "nad pravidlem",
    rungDefault: "výchozí",
    rungAttributeTitle: "Pojmenovaný atribut",
    rungAttributeBody:
      "Pojmenujte jeden a jeho hodnota je celá odpověď všude, kde ji záznam nese — odesílatel, "
      + "který už ví, jak se jeho potíže seskupují, porazí cokoli, co Bugler vydestiluje. Kde ho "
      + "záznam nenese, rozhodne pravidlo níž. Prázdné pole znamená jen pravidlo.",
    rungStackTitle: "Kód, který chybu vyhodil",
    rungStackBody:
      "Rámce z exception.stacktrace, hašované spolu s exception.type. Dvě místa v kódu, která "
      + "logují tutéž větu, zůstanou dva druhy potíží; jedna chyba dosažená dvakrát zůstane jedna.",
    rungFailureTitle: "Druh selhání",
    rungFailureBody:
      "exception.type a šablona zprávy, stack se ignoruje. Každý timeout v aplikaci se sejde v "
      + "jedné epizodě, ať byl vyhozen kdekoli.",
    rungMessageTitle: "Co bylo řečeno",
    rungMessageBody:
      "Šablona zprávy (Serilogu i .NET loggeru), název události, nebo tělo s vynechanými id a "
      + "čísly. Seskupuje podle toho, jaká slova odesílatel zvolil, takže jedna nedbalá obecná "
      + "věta slije nesouvisející selhání dohromady.",
    ruleNote:
      "„Seskupovat podle“ určuje, na kterém příčli žebřík začíná. Nad zvolenou příčlí se nekouká "
      + "nikam kromě pojmenovaného atributu, který ji přebíjí vždycky.",
    degradeNote:
      "Co přečíst nejde, spadne o příčku níž a řekne to: neznámý runtime, stack, ve kterém recept "
      + "Bugleru nenajde rámce, záznam bez stacku vůbec — epizoda dostane značku „hruběji“, a "
      + "stack příliš dlouhý na přečtení celý značku „zkrácený stack“. Nikdo na tom není hůř než "
      + "předtím; špatně napsaný parser se projeví viditelným zhrubnutím, nikdy věrohodnou odpovědí.",
    framesLabel: "CO JE RÁMEC, KDYŽ SE ODEČTE ŠUM",
    framesRawCaption: "JAK TO PŘIJDE",
    framesKeptCaption: "CO SE HAŠUJE",
    framesNote:
      "Hlavička jde pryč, protože nese vlastní zprávu výjimky — tady jméno stroje a číslo "
      + "transakce, což by razilo nový druh potíží při každém výskytu. Stejně tak Caused by:, "
      + "„… 12 more“, opsané zdrojové řádky Pythonu, cesty k souborům a čísla řádků. Každý běh "
      + "číslic se vynechá, takže deploy, který posunul řádky, nerozdělí jednu potíž na dvě, a "
      + "běhy stejných rámců se sloučí, takže rekurze jakékoli hloubky je jedna chyba.",
    runtimesNote:
      "Jak se stack trace píše, je věc každého runtimu, takže se recept vybírá podle "
      + "telemetry.sdk.language, které vaše SDK už posílá: dotnet, java, kotlin, nodejs, webjs, "
      + "python, go, php a ruby ho mají. Cokoli jiného spadne o příčku níž, místo aby hádalo.",
    scopeLabel: "KAM AŽ JEDNA EPIZODA SAHÁ",
    scopeAlways:
      "Epizodu vždycky ohraničuje aplikace. Nad ní zaškrtněte ty stránky odesílatele, které musí "
      + "sedět, aby dva záznamy jednoho druhu sdílely epizodu.",
    byEnvironment: "Prostředí",
    byNamespace: "Namespace",
    byServiceName: "Název služby",
    scopeEnvNote:
      "Doporučeno. Staging a produkce sdílejí kód a tím i otisky; slité dohromady krmí padající "
      + "testovací běh epizodu donekonečna a produkční potíž nikdy neutichne.",
    scopeNsNote:
      "Zaškrtněte, aby tenanti — nebo cokoli, co váš namespace pojmenovává — měli vlastní epizody.",
    scopeNameNote:
      "Zaškrtněte, aby každá role zůstala zvlášť: api a worker jednoho nasazení pak nesdílejí "
      + "epizodu ani při téže chybě ve sdíleném kódu.",
    scopeExample:
      "Se zaškrtnutým jen prostředím je jedna chyba v deseti zákaznických nasazeních produkce "
      + "jedna epizoda s deseti účastmi — jedno upozornění, jedno převzetí, jeden verdikt — "
      + "zatímco staging si nechá svou.",
    repartitionNote:
      "Změna kteréhokoli z nich přerozdělí to, co je otevřené: takové epizody se ztlumí a "
      + "vyladěná tichá okna zahodí. Karta se před uložením zeptá.",
    gotIt: "Rozumím",
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
    claimLeaseLabel: "Lease strojního převzetí (h)",
    claimLeaseHelp:
      "Jak dlouho drží strojní převzetí epizody, než zvadne, pokud ho agent neobnoví — "
      + "spadlý agent epizodu vrátí nejpozději po tolika hodinách. Ponechte prázdné pro "
      + "výchozí hodnotu.",
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
