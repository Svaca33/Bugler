import { plural } from "../plural";
import type { ExploreMessages } from "../sections/explore";

export const explore: ExploreMessages = {
  refresh: "Obnovit",

  loadFailed: {
    logs: "Nepodařilo se načíst logy",
    logCount: "Nepodařilo se spočítat záznamy logů",
    logRecord: "Nepodařilo se načíst záznam logu",
    traces: "Nepodařilo se načíst traces",
    trace: "Nepodařilo se načíst trace",
    volume: "Nepodařilo se sumarizovat objem logů",
    observedKeys: "Nepodařilo se načíst pozorované klíče",
  },

  listError: {
    title: "Tento seznam se nepodařilo načíst.",
    retry: "Zkusit znovu",
  },

  errorSearchTruncated:
    "Hledání skončilo dřív, než došlo na konec okna — starších chybných tras může být víc, než je "
    + "níže vidět. Zužte časový rozsah nebo zdroj, ať se prohledává menší okno.",

  rail: {
    scopeGroup: "ZAMĚŘENÍ",
    sourceGroup: "ZDROJ",
    timeGroup: "ČAS",
    severityGroup: "ZÁVAŽNOST",
    messageGroup: "ZPRÁVA",
    statusGroup: "STAV",
    attributesGroup: "ATRIBUTY",
    allApplications: "Všechny aplikace",
    allNamespaces: "Všechny namespaces",
    allEnvironments: "Všechna prostředí",
    allServices: "Všechny služby",
    traceScope: shortId => `Trace: ${shortId}…`,
    leaveTrace: "Opustit trace",
    searchPlaceholder: "Hledat ve zprávě…",
    search: "Hledat",
    errorsOnly: "Pouze chyby",
  },

  attributes: {
    addFilter: "+ Filtr",
    keyPlaceholder: "Filtrovat podle klíče…",
    loadingKeys: "Načítání klíčů…",
    noKeysObserved: "Žádné pozorované klíče.",
    valuePlaceholder: "Hodnota",
    add: "Přidat",
    cancelFilter: "Zrušit filtr",
    attributesScope: "Atributy",
    resourceScope: "Atributy zdroje",
    filterBy: label => `Filtrovat podle ${label}`,
    stopFilteringBy: label => `Přestat filtrovat podle ${label}`,
    filterByValueTitle: "Filtrovat podle této hodnoty",
    removeFilterTitle: "Odebrat tento filtr",
  },

  logs: {
    title: "Logy",
    loaded: count => `Načteno ${count}`,
    counted: (shown, total, capped) =>
      `${shown} z ${total}${capped ? "+" : ""} ${plural("cs", total, {
        one: "záznamu",
        few: "záznamů",
        other: "záznamů",
      })}`,
    follow: "Sledovat",
    following: "Sledovat ●",
    headers: {
      time: "ČAS",
      severity: "ZÁVAŽNOST",
      service: "SLUŽBA",
      tenant: "TENANT",
      message: "ZPRÁVA",
    },
    loadOlder: "Načíst starší",
    noOlderRecords: "Žádné starší záznamy",
  },

  logDetail: {
    title: "Záznam logu",
    rows: {
      time: "Čas",
      severity: "Závažnost",
      service: "Služba",
      scope: "Scope",
      span: "Span",
    },
    viewTrace: shortId => `Zobrazit trace ${shortId}…`,
  },

  sections: {
    message: "Zpráva",
    attributes: "Atributy",
    resource: "Atributy zdroje",
    events: "Události",
  },

  traces: {
    title: "Traces",
    count: count =>
      plural("cs", count, {
        one: `${count} trace`,
        other: `${count} traces`,
      }),
    headers: {
      rootSpan: "KOŘENOVÝ SPAN",
      service: "SLUŽBA",
      started: "ZAČÁTEK",
      duration: "DOBA TRVÁNÍ",
      spans: "SPANY",
      status: "STAV",
    },
  },

  traceDetail: {
    loading: "Načítání trace…",
    notFound: "Trace nebyl nalezen (nebo pro vás není viditelný).",
    spansCount: count =>
      plural("cs", count, {
        one: `${count} span`,
        few: `${count} spany`,
        other: `${count} spanů`,
      }),
    errorsCount: count =>
      plural("cs", count, {
        one: `${count} chyba`,
        few: `${count} chyby`,
        other: `${count} chyb`,
      }),
    viewCorrelatedLogs: "Zobrazit související logy",
    headers: {
      span: "SPAN",
      timeline: totalMs => `ČASOVÁ OSA · 0 → ${totalMs} ms`,
      duration: "DOBA TRVÁNÍ",
    },
    rows: {
      span: "Span",
      parent: "Nadřazený",
      kind: "Druh",
      service: "Služba",
      start: "Začátek",
      status: "Stav",
    },
  },

  volume: {
    tooWide: "Okno je příliš široké na souhrn. Zužte časový filtr — seznamu níže se to nedotkne.",
    windowLabel: (from, to, width) => `${from} → ${to} · buckety po ${width}`,
    summarizing: "Sumarizace objemu",
    bucketPartlyInside: span => `${span} · jen zčásti uvnitř okna`,
    bucketElapsing: span => `${span} · stále probíhá`,
    bucketCut: span => `${span} · oříznut oknem`,
    releasesCount: count =>
      plural("cs", count, {
        one: `${count} release`,
        few: `${count} releasy`,
        other: `${count} releasů`,
      }),
  },

  timeFilter: {
    timeRangeAria: "Časový rozsah",
    allTime: "Celá doba",
    custom: "Vlastní…",
    edit: "Upravit",
    last: "Posledních",
    amountAria: "Počet",
    unitAria: "Jednotka",
    apply: "Použít",
    applyRelativeAria: "Použít relativní rozsah",
    applyAbsoluteAria: "Použít absolutní rozsah",
    from: "Od",
    to: "Do",
    toInline: "do",
    cancelAria: "Zrušit časový rozsah",
    endsBeforeItStarts: "Tento rozsah končí dříve, než začíná.",
    hint: "Prázdné konce zůstávají otevřené. Časy jsou ve vašem místním časovém pásmu.",
    fromInstant: time => `Od ${time}`,
    untilInstant: time => `Do ${time}`,
    subjects: {
      logRecords: "logy",
      traces: "traces",
    },
    noneInLast: (subject, window) => `Žádné ${subject} za ${window.toLowerCase()}.`,
    noneIn: (subject, window) => `Žádné ${subject} v rozsahu ${window}.`,
    noneMatch: subject => `Žádné ${subject} neodpovídají aktuálním filtrům.`,
    widenTo: "Rozšířit na",
  },
};
