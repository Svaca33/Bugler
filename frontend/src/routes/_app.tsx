import { Outlet, createFileRoute, redirect, useNavigate, useRouter } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import { currentUserQuery } from "@/api/queries";
import markDark from "@/bugler-mark-dark.svg";
import { Button } from "@/components/ui/button";
import { ChangePasswordDialog } from "@/features/access/ChangePasswordDialog";
import { useCurrentUser, useLogout } from "@/features/access/useAuth";
import { useOpenEpisodeCount } from "@/features/alerting/useOpenEpisodeCount";
import { NavTab } from "./-nav-tab";

export const Route = createFileRoute("/_app")({
  /**
   * The whole gate: everything a signed-in person may see hangs under this route, so this is the
   * only place that has to ask. Turning somebody away carries the address they were heading for,
   * because a link out of an alert is worth nothing if signing in loses it — and it replaces rather
   * than stacks, so Back leads out to wherever the link was clicked instead of to a sign-in form
   * that would immediately bounce them forward again.
   */
  beforeLoad: async ({ context, location }) => {
    const user = await context.queryClient.ensureQueryData(currentUserQuery);
    if (user == null) {
      throw redirect({ to: "/login", search: { redirect: location.href }, replace: true });
    }
  },
  component: AppShell,
});

function AppShell() {
  const router = useRouter();
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();
  const [changingPassword, setChangingPassword] = useState(false);
  const openEpisodes = useOpenEpisodeCount(user.data != null);

  // The gate runs on navigation, and a Session can end without one — expiring while the tab sits in
  // the background. When the answer changes underneath us, ask the router to run the gate again
  // rather than deciding here what a signed-out visitor gets: that rule lives in one place.
  //
  // Signing out is the one way it ends that is nobody's surprise: it is already on its way to the
  // sign-in page, and running the gate at it would pin the page just deliberately left to the URL
  // as somewhere to come back to.
  useEffect(() => {
    if (user.data === null && logout.isIdle) {
      void router.invalidate();
    }
  }, [user.data, logout.isIdle, router]);

  if (user.data == null) {
    return null;
  }

  return (
    <div className="flex h-screen flex-col">
      <header className="flex h-[54px] shrink-0 items-stretch border-b border-[#17293D] bg-sidebar px-[22px]">
        <div className="flex items-center gap-[9px] pr-[30px]">
          <img src={markDark} alt="" className="size-7" />
          <span className="text-[21px] leading-none font-semibold tracking-[-0.85px]">bugler</span>
        </div>

        <nav className="flex items-stretch gap-0.5">
          <NavTab to="/dashboard" label="Dashboard" />
          <NavTab to="/" label="Logs" />
          <NavTab to="/traces" label="Traces" />
          <NavTab to="/episodes" label="Episodes" badge={openEpisodes} />
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
