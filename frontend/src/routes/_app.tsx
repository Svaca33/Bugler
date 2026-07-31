import { Navigate, Outlet, createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import markDark from "@/bugler-mark-dark.svg";
import { Button } from "@/components/ui/button";
import { ChangePasswordDialog } from "@/features/access/ChangePasswordDialog";
import { useCurrentUser, useLogout } from "@/features/access/useAuth";
import { useOpenEpisodeCount } from "@/features/alerting/useOpenEpisodeCount";
import { NavTab } from "./-nav-tab";

export const Route = createFileRoute("/_app")({
  component: AppShell,
});

function AppShell() {
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();
  const [changingPassword, setChangingPassword] = useState(false);
  const openEpisodes = useOpenEpisodeCount(user.data != null);

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
        </div>

        <nav className="flex items-stretch gap-0.5">
          <NavTab to="/" label="Logs" />
          <NavTab to="/traces" label="Traces" />
          <NavTab to="/alerts" label="Alerts" badge={openEpisodes} />
          {user.data.isAdmin && <NavTab to="/admin" label="Admin" />}
        </nav>

        <div className="ml-auto flex items-center gap-3.5">
          {/* Your own e-mail is where your account settings live — there is no other door. */}
          <button
            type="button"
            className="max-w-[240px] truncate rounded-[5px] px-1.5 py-1 font-mono text-[11.5px] text-[#A9BDD1] hover:bg-[#12253A] hover:text-[#DCE8F3]"
            title={`${user.data.email} — change password`}
            onClick={() => setChangingPassword(true)}
          >
            {user.data.email}
          </button>
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

      <ChangePasswordDialog open={changingPassword} onOpenChange={setChangingPassword} />
    </div>
  );
}
