import { Navigate, createFileRoute } from "@tanstack/react-router";

import { UsersAdminPage } from "@/features/access/UsersAdminPage";
import { useCurrentUser } from "@/features/access/useAuth";
import { CatalogAdminPage } from "@/features/registry/CatalogAdminPage";

export const Route = createFileRoute("/_app/admin")({
  component: AdminRoute,
});

function AdminRoute() {
  const user = useCurrentUser();

  if (user.isPending) {
    return null;
  }

  if (user.data?.isAdmin !== true) {
    return <Navigate to="/" />;
  }

  return (
    <div className="grid gap-6 overflow-auto p-4">
      <CatalogAdminPage />
      <UsersAdminPage />
    </div>
  );
}
