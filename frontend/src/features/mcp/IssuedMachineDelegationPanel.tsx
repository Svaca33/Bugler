import { useState } from "react";

import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";

import { connectCommand, type IssuedMachineDelegation } from "./useMachineDelegations";

const CAPTION = "font-mono text-[11px] tracking-[0.08em] text-[#7D93AA]";

/**
 * The one moment the Secret exists anywhere Bugler can show it (ADR 0029). It is shown inside the
 * command rather than on its own, because a bare string invites being pasted somewhere it does not
 * belong — and the command is what the person actually needs.
 */
export function IssuedMachineDelegationPanel(props: {
  issued: IssuedMachineDelegation;
  publicUrl: string;
  onDone: () => void;
}) {
  const t = useT();
  const [copied, setCopied] = useState(false);

  const address = props.publicUrl.length > 0 ? props.publicUrl : t.mcp.settings.addressPlaceholder;
  const command = connectCommand(address, props.issued.secret);

  return (
    <div className="flex max-w-[720px] flex-col gap-4 rounded-[11px] border border-[#2F5A3A] bg-[#0D1B14] p-4">
      <span className={CAPTION}>{t.mcp.issued.title}</span>
      <p className="text-[12.5px] text-[#8CA1B8]">{t.mcp.issued.description}</p>

      <div className="flex flex-col gap-1.5">
        <span className="text-[12px] text-[#A9BDD1]">{t.mcp.issued.commandLabel}</span>
        <code className="block overflow-x-auto rounded-[7px] border border-[#17293D] bg-[#081118] px-3 py-2.5 font-mono text-[11.5px] whitespace-pre text-[#DCE8F3]">
          {command}
        </code>
        <span className="text-[11.5px] text-[#7D93AA]">{t.mcp.issued.commandHint}</span>
      </div>

      <div className="flex gap-2">
        <Button
          type="button"
          onClick={() => {
            void navigator.clipboard.writeText(command).then(() => setCopied(true));
          }}
        >
          {copied ? t.mcp.issued.copied : t.mcp.issued.copy}
        </Button>
        <Button type="button" variant="outline" onClick={props.onDone}>
          {t.mcp.issued.done}
        </Button>
      </div>
    </div>
  );
}
