import type { CommonMessages } from "../sections/common";

export const common: CommonMessages = {
  loading: "Loading…",
  loadFailed: {
    session: "Failed to load session",
    releases: "Failed to load releases",
    catalog: "Failed to load catalog",
  },
  rangePreset: {
    PT5M: "Last 5 min",
    PT15M: "Last 15 min",
    PT1H: "Last 1 h",
    P1D: "Last 24 h",
    P7D: "Last 7 d",
    P30D: "Last 30 d",
  },
  lastSpan: parts => `Last ${parts}`,
  underAMinute: "under a minute",
  rangeUnits: {
    minutes: "minutes",
    hours: "hours",
    days: "days",
    weeks: "weeks",
    months: "months",
  },
  severityFilter: {
    all: "All severities",
    infoAndAbove: "Info and above",
    warnAndAbove: "Warn and above",
    errorAndAbove: "Error and above",
  },
};
