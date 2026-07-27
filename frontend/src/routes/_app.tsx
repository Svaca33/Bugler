import { Link, Navigate, Outlet, createFileRoute, useNavigate } from "@tanstack/react-router";

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
    <div className="flex h-screen flex-col">
      <header className="flex items-center gap-6 border-b px-4 py-2">
        <span className="text-lg font-bold">🐛 Bugler</span>
        <nav className="flex items-center gap-1">
          <NavLink to="/" label="Logs" />
          <NavLink to="/traces" label="Traces" />
          {user.data.isAdmin && <NavLink to="/admin" label="Admin" />}
        </nav>
        <div className="ml-auto flex items-center gap-3">
          <span className="text-sm text-muted-foreground">{user.data.email}</span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => logout.mutate(undefined, { onSuccess: () => navigate({ to: "/login" }) })}
          >
            Sign out
          </Button>
        </div>
      </header>
      <main className="min-h-0 flex-1">
        <Outlet />
      </main>
    </div>
  );
}

function NavLink(props: { to: string; label: string }) {
  return (
    <Link
      to={props.to}
      className="rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground hover:bg-accent hover:text-foreground [&.active]:bg-accent [&.active]:text-foreground"
      activeOptions={{ exact: props.to === "/" }}
    >
      {props.label}
    </Link>
  );
}
