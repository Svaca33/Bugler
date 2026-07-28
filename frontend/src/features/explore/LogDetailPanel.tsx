import { Link } from "@tanstack/react-router";

import type { LogRecord } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";

import { removeFilter, upsertFilter, type AttributeFilter } from "./attributeFilters";
import { AttributeLeafList } from "./AttributeLeafList";
import { formatTime } from "./format";
import { severityClass, severityLabel } from "./severity";
import { serviceLabels } from "./sourceFilter";

export function LogDetailPanel(props: {
  log: LogRecord;
  filters: AttributeFilter[];
  onFiltersChange: (filters: AttributeFilter[]) => void;
  onClose: () => void;
}) {
  const { log, filters, onFiltersChange } = props;
  const catalog = useCatalog();
  const toggle = (filter: AttributeFilter, active: boolean) =>
    onFiltersChange(active ? removeFilter(filters, filter) : upsertFilter(filters, filter));
  const severity = Number(log.severityNumber);
  const service = serviceLabels(catalog.data?.applications ?? []).get(log.serviceId);
  return (
    <aside className="flex w-96 shrink-0 flex-col gap-[18px] overflow-auto border-l border-[#17293D] bg-[#0B1826] px-5 py-4">
      <div className="flex items-center gap-2">
        <h2 className="text-sm font-semibold tracking-[-0.1px]">Log record</h2>
        <span className="font-mono text-sm font-medium text-[#A9BDD1]">#{log.id}</span>
        <button
          type="button"
          aria-label="Close"
          className="ml-auto rounded-[5px] px-1.5 py-0.5 text-[13px] text-muted-foreground hover:bg-[#16283C] hover:text-foreground"
          onClick={props.onClose}
        >
          ✕
        </button>
      </div>

      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-[5px]">
        <Row label="Time" value={formatTime(log.timestamp)} />
        <Row
          label="Severity"
          value={log.severityText || severityLabel(severity)}
          valueClass={severityClass(severity)}
        />
        <Row label="Service" value={service ?? "—"} />
        <Row label="Scope" value={log.scopeName ?? "—"} />
        <Row label="Span" value={log.spanId ?? "—"} />
      </dl>

      {log.traceId != null && (
        <Button asChild variant="secondary" size="sm" className="w-full">
          <Link to="/traces/$traceId" params={{ traceId: log.traceId }}>
            View trace {log.traceId.slice(0, 8)}…
          </Link>
        </Button>
      )}

      <Section title="Message">
        <pre className="whitespace-pre-wrap break-words rounded-[7px] bg-[#16283C] p-2.5 font-mono text-[11.5px] leading-[1.55] text-[#DCE8F3]">
          {log.body}
        </pre>
      </Section>
      <Section title="Attributes">
        <AttributeLeafList
          attributes={log.attributes}
          scope="attribute"
          filters={filters}
          onToggle={toggle}
        />
      </Section>
      <Section title="Resource">
        <AttributeLeafList
          attributes={log.resourceAttributes}
          scope="resource"
          filters={filters}
          onToggle={toggle}
        />
      </Section>
    </aside>
  );
}

function Row(props: { label: string; value: string; valueClass?: string }) {
  return (
    <>
      <dt className="text-[12.5px] text-muted-foreground">{props.label}</dt>
      <dd className={`break-all font-mono text-[11.5px] leading-5 ${props.valueClass ?? "text-[#DCE8F3]"}`}>
        {props.value}
      </dd>
    </>
  );
}

function Section(props: { title: string; children: React.ReactNode }) {
  return (
    <section className="grid gap-1.5">
      <h3 className="font-mono text-[10px] uppercase tracking-[0.12em] text-[#5F7590]">
        {props.title}
      </h3>
      {props.children}
    </section>
  );
}

export function JsonBlock(props: { value: unknown }) {
  return (
    <pre className="overflow-auto rounded-[7px] bg-[#16283C] p-2.5 font-mono text-[11.5px] leading-[1.5] text-[#C6D6E6]">
      {JSON.stringify(props.value ?? {}, null, 2)}
    </pre>
  );
}
