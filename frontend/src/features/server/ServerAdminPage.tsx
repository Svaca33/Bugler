import { useMutation } from "@tanstack/react-query";

import { api } from "@/api/client";
import { Button } from "@/components/ui/button";

const CAPTION = "font-mono text-[11px] tracking-[0.08em] text-[#7D93AA]";

/** Deployment diagnostics: things that are true of this server rather than of any application. */
export function ServerAdminPage() {
  return (
    <div className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex flex-col gap-1">
        <h2 className="text-[17px] font-semibold tracking-[-0.3px]">Server</h2>
        <p className="text-[12.5px] text-[#8CA1B8]">
          Whether this deployment can do what it promises.
        </p>
      </div>

      <MailCard />
    </div>
  );
}

/**
 * Mail is the one part of Bugler that fails silently. Alerts and reset links leave through a
 * queue, and a relay that refuses them says so only in the container log — so a misconfigured
 * server looks exactly like a quiet week until the first incident goes unannounced. This asks it
 * outright, and reports whatever the mail server actually said.
 */
function MailCard() {
  const send = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/admin/mail/test", {});
      if (error !== undefined) {
        throw new Error(problemDetail(error) ?? "The server refused the message.");
      }
      return data;
    },
  });

  return (
    <div className="flex flex-col gap-3 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <span className={CAPTION}>MAIL</span>
      <p className="max-w-[62ch] text-[12.5px] text-[#8CA1B8]">
        Sends a message to your own account address. If it arrives, alerts and password-reset links
        will reach their recipients too.
      </p>

      <div className="flex items-center gap-3">
        <Button
          type="button"
          size="sm"
          variant="secondary"
          disabled={send.isPending}
          onClick={() => send.mutate()}
        >
          {send.isPending ? "Sending…" : "Send a test message"}
        </Button>

        {send.isSuccess && send.data !== undefined && (
          <span className="text-[12.5px] text-[#8CA1B8]">
            Sent to <code className="font-mono text-[12px]">{send.data.sentTo}</code>.
          </span>
        )}
      </div>

      {send.isError && (
        <div className="flex flex-col gap-1 rounded-[9px] border border-[rgba(229,84,74,0.45)] bg-[rgba(229,84,74,0.10)] p-3">
          <p className="text-[12.5px] font-semibold text-[#F0685A]">The message could not be sent.</p>
          {/* The refusal itself: an operator setting this up would otherwise go to the container log for it. */}
          <code className="font-mono text-[11.5px] break-words text-[#F6C7C2]">
            {send.error.message}
          </code>
        </div>
      )}
    </div>
  );
}

/** ProblemDetails carries what actually went wrong; anything else is not worth guessing at. */
function problemDetail(error: unknown): string | undefined {
  if (typeof error !== "object" || error === null) {
    return undefined;
  }

  const problem = error as { detail?: unknown; title?: unknown };
  if (typeof problem.detail === "string") {
    return problem.detail;
  }

  return typeof problem.title === "string" ? problem.title : undefined;
}
