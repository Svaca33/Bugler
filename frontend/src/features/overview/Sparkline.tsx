import { useT } from "@/i18n";

import type { VolumeBucket } from "./serviceOverview";
import { windowStamp } from "./stamps";

/**
 * Plain flex divs, deliberately not `Chart`: a Recharts instance per card would be forty SVG
 * trees on a screen whose whole job is to be scanned in two seconds, and none of the axes,
 * tooltips or legends a chart brings are wanted here.
 *
 * Each service is normalised against itself: bar heights say nothing across cards, the counts
 * above them do. That is intended, and it is why the counts sit above the sparkline, not below.
 */
export function Sparkline(props: { buckets: VolumeBucket[] | undefined; mini?: boolean }) {
  const t = useT();
  const mini = props.mini === true;
  const buckets = props.buckets ?? placeholder(mini ? 18 : 24);
  const max = Math.max(...buckets.map(total), 1);

  return (
    <div className={mini ? "flex h-5 items-end gap-[1px]" : "flex h-[30px] items-end gap-[2px]"}>
      {buckets.map(bucket => (
        <div
          key={bucket.start}
          className={`${mini ? "min-w-[1px]" : "min-w-[2px]"} flex-1 rounded-[1px]`}
          style={{
            // The 8% floor keeps an empty stretch legible as a floor rather than as missing data.
            height: `${Math.max(8, Math.round((total(bucket) / max) * 100))}%`,
            background: bucket.error > 0 ? "var(--severity-error-fill)" : "#274563",
          }}
          title={
            props.buckets === undefined
              ? undefined
              : t.overview.sparklineBar(
                  windowStamp(bucket.start), bucket.error, bucket.warn, total(bucket),
                )
          }
        />
      ))}
    </div>
  );
}

function total(bucket: VolumeBucket): number {
  return bucket.error + bucket.warn + bucket.info + bucket.debug;
}

function placeholder(count: number): VolumeBucket[] {
  return Array.from({ length: count }, (_, index) => ({
    start: String(index), error: 0, warn: 0, info: 0, debug: 0,
  }));
}
