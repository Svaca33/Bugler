import { Navigate, Outlet, createFileRoute, useNavigate } from "@tanstack/react-router";

import markDark from "@/bugler-mark-dark.svg";
import { Button } from "@/components/ui/button";
import { useCurrentUser, useLogout } from "@/features/access/useAuth";
import { NavTab } from "./-nav-tab";

export const Route = createFileRoute("/_app")({
  component: AppShell,
});

function AppShell() {
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();

  if (user.isPending) {
    return <div className="grid min-h-screen place-items-center text-muted-foreground">Loading…</div>;
  }

  if (user.data == null) {
    return <Navigate to="/login" />;
  }

  return (
    <div className="flex h-screen flex-col">
      <header className="flex h-[54px] shrink-0 items-stretch border-b border-[#17293D] bg-sidebar px-[22px]">
        <div className="flex items-center gap-[9px] pr-[30px]">
          <img src={markDark} alt="" className="size-7" />
          <span className="text-[21px] leading-none font-semibold tracking-[-0.85px]">bugler</span>
          <span className="rounded border border-[#1E344C] px-[5px] py-0.5 font-mono text-[10px] text-[#6E86A0]">
            0.1.0
          </span>
        </div>

        <nav className="flex items-stretch gap-0.5">
          <NavTab to="/" label="Logs" />
          <NavTab to="/traces" label="Traces" />
          <NavTab to="/alerts" label="Alerts" />
          {user.data.isAdmin && <NavTab to="/admin" label="Admin" />}
        </nav>

        <div className="ml-auto flex items-center gap-3.5">
          <span
            className="max-w-[240px] truncate font-mono text-[11.5px] text-[#A9BDD1]"
            title={user.data.email}
          >
            {user.data.email}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => logout.mutate(undefined, { onSuccess: () => navigate({ to: "/login" }) })}
          >
            Sign out
          </Button>
        </div>
      </header>

      <main className="min-h-0 min-w-0 flex-1">
        <Outlet />
      </main>
    </div>
  );
}
