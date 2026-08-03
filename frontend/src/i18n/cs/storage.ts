import type { StorageMessages } from "../sections/storage";

export const storage: StorageMessages = {
  title: "Úložiště",
  subtitle:
    "Kolik telemetrie každé služby stojí a jak rychle roste. Bajty označené ≈ jsou odhady; " +
    "rychlost je měřena za poslední den.",
  rateUnitAria: "Jednotka rychlosti příjmu",
  rateUnit: {
    second: "za sekundu",
    minute: "za minutu",
    hour: "za hodinu",
    day: "za den",
    week: "za týden (projekce)",
    month: "za měsíc (projekce)",
  },
  projectedSuffix: " (projekce)",
  refresh: "Obnovit",
  measuring: "Měření…",
  measuringTables: "Měření tabulek — na vytížené instanci to může chvíli trvat.",
  reportFailed: "Přehled úložiště neodpověděl. Instance může být vytížená.",
  tryAgain: "Zkusit znovu",
  nothingRegistered: "Zatím není nic zaregistrováno.",
  onDisk: (logs, traces, together) => `Logy ${logs} · Traces ${traces} · celkem ${together} na disku`,
  onDiskIncluded: "— včetně tabulek, indexů a TOAST",
  columns: {
    service: "SLUŽBA",
    logs: "LOGY",
    traces: "TRACES",
    total: "CELKEM",
    kept: "RETENCE",
    ingest: unit => `PŘÍJEM ${unit}`,
    settlesAt: "USTÁLÍ SE NA",
  },
  retentionPairTitle: "Retence logů / retence traces",
  ingestSplit: (logs, traces) => `logy ≈ ${logs} · traces ≈ ${traces}`,
  settlesTitle:
    "Kde se objem dat ustálí, pokud vydrží tempo posledního dne — každá retence běží zvlášť",
  settledAtPace: (logDays, traceDays) =>
    `při tempu posledního dne s logy drženými ${logDays} d a traces ${traceDays} d`,
  projectedFootnote: "* projekce z měření za poslední den — dál se nic neměří.",
};
