import { createFileRoute } from "@tanstack/react-router";

import { ForgotPasswordPage } from "@/features/access/ForgotPasswordPage";

export const Route = createFileRoute("/forgot-password")({
  component: ForgotPasswordPage,
});
