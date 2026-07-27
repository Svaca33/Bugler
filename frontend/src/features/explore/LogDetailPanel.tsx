import { Link } from "@tanstack/react-router";

import type { LogRecord } from "@/api/client";
import { Button } from "@/components/ui/button";

import { formatTime } from "./format";
import { severityLabel } from "./severity";

export function LogDetailPanel(props: { log: LogRecord; onClose: () => void }) {
  const { log } = props;
  return (
    <aside className="flex w-96 shrink-0 flex-col gap-3 overflow-auto border-l bg-background p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">Log record #{log.id}</h2>
        <Button variant="ghost" size="sm" onClick={props.onClose}>
          ✕
        </Button>
      </div>

      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <Row label="Time" value={formatTime(log.timestamp)} />
        <Row label="Severity" value={log.severityText || severityLabel(Number(log.severityNumber))} />
        <Row label="Service" value={log.serviceName ?? "—"} />
        <Row label="Scope" value={log.scopeName ?? "—"} />
        <Row label="Span" value={log.spanId ?? "—"} />
      </dl>

      {log.traceId != null && (
        <Button asChild variant="secondary" size="sm">
          <Link to="/traces/$traceId" params={{ traceId: log.traceId }}>
            View trace {log.traceId.slice(0, 8)}…
          </Link>
        </Button>
      )}

      <Section title="Message">
        <pre className="whitespace-pre-wrap break-words rounded bg-muted p-2 font-mono text-xs">
          {log.body}
        </pre>
      </Section>
      <Section title="Attributes">
        <JsonBlock value={log.attributes} />
      </Section>
      <Section title="Resource">
        <JsonBlock value={log.resourceAttributes} />
      </Section>
    </aside>
  );
}

function Row(props: { label: string; value: string }) {
  return (
    <>
      <dt className="text-muted-foreground">{props.label}</dt>
      <dd className="break-all font-mono text-xs leading-5">{props.value}</dd>
    </>
  );
}

function Section(props: { title: string; children: React.ReactNode }) {
  return (
    <section className="grid gap-1">
      <h3 className="text-xs font-medium uppercase text-muted-foreground">{props.title}</h3>
      {props.children}
    </section>
  );
}

export function JsonBlock(props: { value: unknown }) {
  return (
    <pre className="overflow-auto rounded bg-muted p-2 font-mono text-xs">
      {JSON.stringify(props.value ?? {}, null, 2)}
    </pre>
  );
}
