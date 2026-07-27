import { createFileRoute } from "@tanstack/react-router";

import { LoginPage } from "@/features/access/LoginPage";

export const Route = createFileRoute("/login")({
  component: LoginPage,
});
