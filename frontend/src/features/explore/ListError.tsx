import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";

/**
 * What a list shows in place of its rows when the query failed. Deliberately not the empty state:
 * "nothing matched" is an answer about production and "this Bugler could not ask" is an answer
 * about itself, and a reader looking for an outage has to be able to tell the two apart.
 */
export function ListError(props: { message: string; onRetry: () => void }) {
  const t = useT();

  return (
    <div className="flex flex-col items-center gap-3 py-16">
      <p className="text-[#F0685A]">{t.explore.listError.title}</p>
      <p className="text-xs text-[#8CA1B8]">{props.message}</p>
      <Button variant="outline" size="sm" onClick={props.onRetry}>
        {t.explore.listError.retry}
      </Button>
    </div>
  );
}
