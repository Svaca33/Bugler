import { Navigate, createFileRoute, useNavigate } from "@tanstack/react-router";

import { UsersAdminPage } from "@/features/access/UsersAdminPage";
import { useCurrentUser } from "@/features/access/useAuth";
import { CatalogAdminPage } from "@/features/registry/CatalogAdminPage";
import { ServerAdminPage } from "@/features/server/ServerAdminPage";
import { StorageAdminPage } from "@/features/storage/StorageAdminPage";

type AdminSection = "topology" | "storage" | "people" | "server";

const SECTIONS: AdminSection[] = ["topology", "storage", "people", "server"];

export const Route = createFileRoute("/_app/admin")({
  validateSearch: (search: Record<string, unknown>): { section: AdminSection } => ({
    section: SECTIONS.find(s => s === search.section) ?? "topology",
  }),
  component: AdminRoute,
});

function AdminRoute() {
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
          <h1 className="text-[19px] font-semibold tracking-[-0.4px]">Administration</h1>
          <p className="text-[12.5px] text-[#8CA1B8]">What sends telemetry, and who may read it.</p>
        </div>
        <nav className="ml-auto flex gap-0.5">
          <Tab
            label="Topology"
            active={section === "topology"}
            onClick={() => navigate({ search: { section: "topology" }, replace: true })}
          />
          <Tab
            label="Storage"
            active={section === "storage"}
            onClick={() => navigate({ search: { section: "storage" }, replace: true })}
          />
          <Tab
            label="People"
            active={section === "people"}
            onClick={() => navigate({ search: { section: "people" }, replace: true })}
          />
          <Tab
            label="Server"
            active={section === "server"}
            onClick={() => navigate({ search: { section: "server" }, replace: true })}
          />
        </nav>
      </div>

      <div className="min-h-0 flex-1">
        {section === "topology" && <CatalogAdminPage />}
        {section === "storage" && <StorageAdminPage />}
        {section === "people" && <UsersAdminPage />}
        {section === "server" && <ServerAdminPage />}
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
