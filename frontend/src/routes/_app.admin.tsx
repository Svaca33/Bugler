import { Navigate, createFileRoute, useNavigate } from "@tanstack/react-router";

import { UsersAdminPage } from "@/features/access/UsersAdminPage";
import { useCurrentUser } from "@/features/access/useAuth";
import { HeldMachineDelegationsCard } from "@/features/mcp/HeldMachineDelegationsCard";
import { McpSettingsCard } from "@/features/mcp/McpSettingsCard";
import { CatalogAdminPage } from "@/features/registry/CatalogAdminPage";
import { ServerAdminPage } from "@/features/server/ServerAdminPage";
import { StorageAdminPage } from "@/features/storage/StorageAdminPage";
import { useT } from "@/i18n";

type AdminSection = "topology" | "storage" | "people" | "server";

const SECTIONS: AdminSection[] = ["topology", "storage", "people", "server"];

export const Route = createFileRoute("/_app/admin")({
  validateSearch: (search: Record<string, unknown>): { section: AdminSection } => ({
    section: SECTIONS.find(s => s === search.section) ?? "topology",
  }),
  component: AdminRoute,
});

function AdminRoute() {
  const t = useT();
  const user = useCurrentUser();
  const { section } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });

  if (user.isPending) {
    return null;
  }

  if (user.data?.isAdmin !== true) {
    return <Navigate to="/" />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-end gap-6 border-b border-[#17293D] bg-[#0B1826] px-6 pt-5">
        <div className="flex flex-col gap-1 pb-3.5">
          <h1 className="text-[19px] font-semibold tracking-[-0.4px]">
            {t.server.adminShell.title}
          </h1>
          <p className="text-[12.5px] text-[#8CA1B8]">{t.server.adminShell.subtitle}</p>
        </div>
        <nav className="ml-auto flex gap-0.5">
          <Tab
            label={t.server.adminShell.tabs.topology}
            active={section === "topology"}
            onClick={() => navigate({ search: { section: "topology" }, replace: true })}
          />
          <Tab
            label={t.server.adminShell.tabs.storage}
            active={section === "storage"}
            onClick={() => navigate({ search: { section: "storage" }, replace: true })}
          />
          <Tab
            label={t.server.adminShell.tabs.people}
            active={section === "people"}
            onClick={() => navigate({ search: { section: "people" }, replace: true })}
          />
          <Tab
            label={t.server.adminShell.tabs.server}
            active={section === "server"}
            onClick={() => navigate({ search: { section: "server" }, replace: true })}
          />
        </nav>
      </div>

      <div className="min-h-0 flex-1">
        {section === "topology" && <CatalogAdminPage />}
        {section === "storage" && <StorageAdminPage />}
        {section === "people" && <UsersAdminPage />}
        {/*
          The machine door is the Host's, the delegations are Access's, and the rest of this page is
          the deployment's — a page may combine contexts even though the features may not reach
          into one another, and this is where that combining is done.
        */}
        {section === "server" && (
          <ServerAdminPage>
            <McpSettingsCard />
            <HeldMachineDelegationsCard />
          </ServerAdminPage>
        )}
      </div>
    </div>
  );
}

function Tab(props: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={props.onClick}
      className={`rounded-t-lg px-[15px] py-[9px] text-[13px] ${
        props.active
          ? "bg-background text-[#F6C170] shadow-[inset_0_2px_0_#E9A43C]"
          : "text-[#8CA1B8] hover:text-foreground"
      }`}
    >
      {props.label}
    </button>
  );
}
