import { Link, Navigate, Outlet, createFileRoute, useNavigate } from "@tanstack/react-router";

import markDark from "@/bugler-mark-dark.svg";
import { Button } from "@/components/ui/button";
import { useCurrentUser, useLogout } from "@/features/access/useAuth";

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
    <div className="flex h-screen">
      <aside className="flex w-[222px] shrink-0 flex-col gap-[26px] border-r border-[#17293D] bg-sidebar px-4 pt-5 pb-4">
        <div className="flex items-center gap-[9px] px-1">
          <img src={markDark} alt="" className="size-[30px]" />
          <span className="text-[23px] font-semibold tracking-[-0.9px]">bugler</span>
          <span className="ml-auto rounded border border-[#1E344C] px-[5px] py-0.5 font-mono text-[10px] text-[#6E86A0]">
            0.1.0
          </span>
        </div>

        <nav className="flex flex-col gap-0.5">
          <NavLink to="/" label="Logs" />
          <NavLink to="/traces" label="Traces" />
          {user.data.isAdmin && <NavLink to="/admin" label="Admin" />}
        </nav>

        <div className="mt-auto flex flex-col gap-2 rounded-[9px] border border-[#17293D] bg-background p-3">
          <span className="truncate font-mono text-[11.5px] text-[#A9BDD1]" title={user.data.email}>
            {user.data.email}
          </span>
          <Button
            variant="outline"
            size="sm"
            className="w-full"
            onClick={() => logout.mutate(undefined, { onSuccess: () => navigate({ to: "/login" }) })}
          >
            Sign out
          </Button>
        </div>
      </aside>

      <main className="min-h-0 min-w-0 flex-1">
        <Outlet />
      </main>
    </div>
  );
}

function NavLink(props: { to: string; label: string }) {
  return (
    <Link
      to={props.to}
      className="flex items-center gap-2.5 rounded-[7px] px-2.5 py-2 text-[13.5px] text-[#B6C8DA] hover:bg-[#12253A] hover:text-foreground [&.active]:bg-[rgba(233,164,60,0.11)] [&.active]:font-medium [&.active]:text-[#F6C170] [&.active]:shadow-[inset_2px_0_0_#E9A43C]"
      activeOptions={{ exact: props.to === "/" }}
    >
      {props.label}
    </Link>
  );
}
