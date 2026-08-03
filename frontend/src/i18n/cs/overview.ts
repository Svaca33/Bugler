import { FORMAT_LOCALES } from "../language";
import { plural } from "../plural";
import type { OverviewMessages } from "../sections/overview";

export const overview: OverviewMessages = {
  title: "Vaše služby",
  subtitle: "Každá služba, kterou vidíte, a zda má právě teď potíže.",
  controls: {
    windowCaption: "OKNO",
    groupByCaption: "SESKUPIT PODLE",
    troubleOnly: "Pouze služby s potížemi",
    volume: "Objem",
    facet: {
      app: "Aplikace",
      namespace: "Namespace",
      environment: "Prostředí",
      service: "Služba",
    },
    shown: count => `zobrazeno ${count}`,
    cardsTitle: "Karty",
    tableTitle: "Tabulka",
  },
  errors: {
    saveSubscriptions: "Nepodařilo se uložit odběry.",
    unavailable: parts => `Momentálně nedostupné: ${parts}.`,
    volumeAggregate: "objem — počty a minigrafy nic neukazují",
    episodesAggregate: "epizody — stavy nic neukazují",
  },
  empty: {
    noApplication: "Nevidíte žádnou aplikaci.",
    nothingInTrouble: "Žádné potíže — žádná sledovaná služba nemá otevřenou epizodu.",
  },
  allServices: "Všechny služby",
  serviceCount: count =>
    plural("cs", count, {
      one: `${count} služba`,
      few: `${count} služby`,
      other: `${count} služeb`,
    }),
  badge: {
    openEpisodes: count =>
      plural("cs", count, {
        one: "otevřená epizoda",
        few: `${count} otevřené epizody`,
        other: `${count} otevřených epizod`,
      }),
    quietedEpisodes: count =>
      plural("cs", count, {
        one: "utichlá epizoda",
        few: `${count} utichlé epizody`,
        other: `${count} utichlých epizod`,
      }),
    quieted: count =>
      plural("cs", count, {
        one: `${count} utichlá`,
        few: `${count} utichlé`,
        other: `${count} utichlých`,
      }),
    inTrouble: count => `${count} s potížemi`,
    allClear: "vše v pořádku",
  },
  card: {
    caption: {
      errors: "ERRORS",
      warn: "WARN",
      logs: "LOGY",
    },
    now: "teď",
    opened: clock => `otevřena ${clock}`,
    healthCheck: "health check",
    errCount: count => `${count.toLocaleString(FORMAT_LOCALES.cs)} err`,
    warnCount: count => `${count.toLocaleString(FORMAT_LOCALES.cs)} warn`,
  },
  actions: {
    toLogs: "Do logů",
    toTraces: "Do traces",
    toEpisodes: "Do epizod",
  },
  mail: {
    viaApplication: application => `Odebíráno přes aplikaci ${application}`,
    subscribed: "Odebíráno — e-mail, když se otevře nová epizoda",
    notSubscribed: "Bez odběru",
  },
  sparklineBar: (stamp, error, warn, logs) =>
    `${stamp} · ${error.toLocaleString(FORMAT_LOCALES.cs)} err · ` +
    `${warn.toLocaleString(FORMAT_LOCALES.cs)} warn · ` +
    plural("cs", logs, {
      one: `${logs.toLocaleString(FORMAT_LOCALES.cs)} log`,
      few: `${logs.toLocaleString(FORMAT_LOCALES.cs)} logy`,
      other: `${logs.toLocaleString(FORMAT_LOCALES.cs)} logů`,
    }),
  table: {
    header: {
      service: "SLUŽBA",
      status: "STAV",
      errors: "ERRORS",
      warn: "WARN",
      logs: "LOGY",
      volume: "OBJEM",
      latestEpisode: "POSLEDNÍ EPIZODA",
      mail: "MAIL",
    },
    allClearAgo: span => `vše v pořádku už ${span}`,
    // "Posledních 24 h" embeds mid-sentence with its first letter lowered: "za posledních 24 h".
    noEpisodeIn: window => `žádná epizoda za ${window.charAt(0).toLowerCase()}${window.slice(1)}`,
  },
};
