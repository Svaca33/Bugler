import { RotateCwIcon } from "lucide-react";

import { Button } from "@/components/ui/button";

/**
 * Asks the page's questions again, now. It re-runs the queries exactly as filtered: a relative
 * window naturally slides to the present, an absolute one is re-asked as it stands — late-arriving
 * records are reason enough to ask twice about the same stretch of the past.
 */
export function RefreshButton(props: { busy: boolean; onRefresh: () => void }) {
  return (
    <Button
      type="button"
      variant="outline"
      size="icon-sm"
      aria-label="Refresh"
      title="Refresh"
      disabled={props.busy}
      onClick={props.onRefresh}
    >
      <RotateCwIcon className={props.busy ? "animate-spin" : undefined} />
    </Button>
  );
}
