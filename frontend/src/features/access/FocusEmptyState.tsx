import { Link } from "@tanstack/react-router";

import { useCurrentUser } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";

/**
 * What a reading page shows instead of a list while its reader is watching nothing at all. A Focus
 * is silent everywhere else on purpose — but "no telemetry" and "telemetry you asked not to see"
 * must never read the same, and this is the one state where they would.
 *
 * It stands only for an empty Focus. A Focus that simply found nothing this hour is an ordinary
 * empty list and keeps its own words: those are two different emptinesses.
 */
export function FocusEmptyState() {
  const t = useT();

  return (
    <div className="grid h-full min-h-0 place-items-center p-6">
      <div className="flex max-w-[420px] flex-col items-center gap-3 text-center">
        <h2 className="text-[17px] font-semibold tracking-[-0.3px]">{t.access.focus.empty.title}</h2>
        <p className="text-[12.5px] text-[#8CA1B8]">{t.access.focus.empty.description}</p>
        <Button asChild variant="outline" size="sm" className="mt-1">
          <Link to="/account">{t.access.focus.empty.action}</Link>
        </Button>
      </div>
    </div>
  );
}

/**
 * Whether this reader is watching nothing. Undefined while the session is still loading, so a page
 * can hold its own skeleton rather than flash a sentence that may not be true.
 */
export function useIsWatchingNothing(): boolean | undefined {
  const user = useCurrentUser();
  return user.data == null ? undefined : user.data.focusedApplicationIds.length === 0;
}
