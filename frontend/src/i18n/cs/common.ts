import type { CommonMessages } from "../sections/common";

export const common: CommonMessages = {
  loading: "Načítání…",
  loadFailed: {
    session: "Nepodařilo se načíst přihlášení",
    releases: "Nepodařilo se načíst releasy",
    catalog: "Nepodařilo se načíst katalog",
  },
  rangePreset: {
    PT5M: "Posledních 5 min",
    PT15M: "Posledních 15 min",
    PT1H: "Poslední 1 h",
    P1D: "Posledních 24 h",
    P7D: "Posledních 7 d",
    P30D: "Posledních 30 d",
  },
  lastSpan: parts => `Posledních ${parts}`,
  underAMinute: "pod minutu",
  rangeUnits: {
    minutes: "minuty",
    hours: "hodiny",
    days: "dny",
    weeks: "týdny",
    months: "měsíce",
  },
  severityFilter: {
    all: "Všechny závažnosti",
    infoAndAbove: "Info a vyšší",
    warnAndAbove: "Warn a vyšší",
    errorAndAbove: "Error a vyšší",
  },
};
