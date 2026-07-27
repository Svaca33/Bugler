export interface WaterfallSpan {
  spanId: string;
  parentSpanId?: string | null;
  startTime: string;
  endTime: string;
}

export interface WaterfallRow<T extends WaterfallSpan> {
  span: T;
  depth: number;
  /** Bar position within the trace, in percent of total duration. */
  offsetPercent: number;
  widthPercent: number;
  durationMs: number;
}

/**
 * Lays spans out as an indented waterfall: children under their parent (depth),
 * bars positioned on the shared trace timeline. Orphans surface at depth 0.
 */
export function layoutWaterfall<T extends WaterfallSpan>(spans: readonly T[]): WaterfallRow<T>[] {
  if (spans.length === 0) return [];

  const starts = spans.map(s => Date.parse(s.startTime));
  const ends = spans.map(s => Date.parse(s.endTime));
  const traceStart = Math.min(...starts);
  const traceDuration = Math.max(Math.max(...ends) - traceStart, 1);

  const byId = new Map(spans.map(s => [s.spanId, s]));
  const children = new Map<string, T[]>();
  const roots: T[] = [];
  for (const span of spans) {
    const parent = span.parentSpanId ?? undefined;
    if (parent !== undefined && byId.has(parent) && parent !== span.spanId) {
      const list = children.get(parent) ?? [];
      list.push(span);
      children.set(parent, list);
    } else {
      roots.push(span);
    }
  }

  const byStart = (a: T, b: T) => Date.parse(a.startTime) - Date.parse(b.startTime);
  const rows: WaterfallRow<T>[] = [];
  const visit = (span: T, depth: number) => {
    const start = Date.parse(span.startTime);
    const durationMs = Math.max(Date.parse(span.endTime) - start, 0);
    rows.push({
      span,
      depth,
      offsetPercent: ((start - traceStart) / traceDuration) * 100,
      widthPercent: Math.max((durationMs / traceDuration) * 100, 0.5),
      durationMs,
    });
    for (const child of (children.get(span.spanId) ?? []).sort(byStart)) {
      visit(child, depth + 1);
    }
  };

  for (const root of roots.sort(byStart)) {
    visit(root, 0);
  }

  return rows;
}
