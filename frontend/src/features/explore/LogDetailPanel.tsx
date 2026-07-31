import { Link } from "@tanstack/react-router";

import type { LogRecord } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { formatTime } from "@/lib/format";
import { severityClass, severityLabel } from "@/lib/severity";

import { removeFilter, upsertFilter, type AttributeFilter } from "./attributeFilters";
import { AttributeLeafList } from "./AttributeLeafList";
import { DetailPanel } from "@/components/ui/detail-panel";
import { serviceLabels } from "@/lib/sourceFilter";

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
    <DetailPanel
      onClose={props.onClose}
      title={
        <>
          <h2 className="text-sm font-semibold tracking-[-0.1px]">Log record</h2>
          <span className="font-mono text-sm font-medium text-[#A9BDD1]">#{log.id}</span>
        </>
      }
    >
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-[5px]">
        <Row label="Time" value={formatTime(log.timestamp)} />
        <Row
          label="Severity"
          value={log.severityText || severityLabel(severity)}
          valueClass={severityClass(severity)}
        />
        <Row label="Service" value={service ?? "â€”"} />
        <Row label="Scope" value={log.scopeName ?? "â€”"} />
        <Row label="Span" value={log.spanId ?? "â€”"} />
      </dl>

      {log.traceId != null && (
        <Button asChild variant="secondary" size="sm" className="w-full">
          <Link to="/traces/$traceId" params={{ traceId: log.traceId }}>
            View trace {log.traceId.slice(0, 8)}â€¦
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
    </DetailPanel>
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
