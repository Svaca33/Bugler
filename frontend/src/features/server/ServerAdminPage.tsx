import { MailSettingsCard } from "./MailSettingsCard";

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

      <MailSettingsCard />
    </div>
  );
}
