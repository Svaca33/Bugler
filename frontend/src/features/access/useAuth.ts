import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/api/client";
import { authStatusQuery } from "@/api/queries";
import { getMessages } from "@/i18n/runtime";

// The "who is signed in" query moved to @/api/queries (app-wide, not an access feature);
// re-exported here so the access feature keeps one import for its auth hooks.
export { useCurrentUser } from "@/api/queries";

export function useAuthStatus() {
  return useQuery(authStatusQuery);
}

/**
 * What the server says when an address has spent its budget of attempts. It never says whether the
 * address belongs to an account, so neither does this — only when it is worth asking again.
 */
function tooManyAttempts(response: Response) {
  const seconds = Number(response.headers.get("Retry-After"));
  const errors = getMessages().access.errors;
  return new Error(
    Number.isFinite(seconds) && seconds > 0
      ? errors.tooManyAttemptsIn(seconds)
      : errors.tooManyAttempts,
  );
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (credentials: { email: string; password: string; staySignedIn: boolean }) => {
      const { data, response } = await api.POST("/api/auth/login", { body: credentials });
      if (response.status === 429) throw tooManyAttempts(response);
      if (response.status === 401) throw new Error(getMessages().access.errors.invalidCredentials);
      if (data === undefined) throw new Error(getMessages().access.errors.loginFailed);
      return data;
    },
    onSuccess: user => {
      queryClient.setQueryData(["auth", "me"], user);
      queryClient.invalidateQueries({ queryKey: ["auth", "status"] });
    },
  });
}

export function useSetup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: { email: string; password: string; displayName?: string }) => {
      const { data, response } = await api.POST("/api/auth/setup", {
        body: { email: input.email, password: input.password, displayName: input.displayName ?? null },
      });
      if (response.status === 409) {
        throw new Error(getMessages().access.errors.setupAlreadyCompleted);
      }
      if (data === undefined) throw new Error(getMessages().access.errors.setupFailed);
      return data;
    },
    onSuccess: user => {
      queryClient.setQueryData(["auth", "me"], user);
      queryClient.invalidateQueries({ queryKey: ["auth", "status"] });
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: async (input: { currentPassword: string; newPassword: string }) => {
      const { error, response } = await api.POST("/api/auth/password/change", { body: input });
      if (response.status === 400) {
        // The endpoint answers with the reason: a wrong current password, or a new one it refused.
        throw new Error(
          typeof error === "string" ? error : getMessages().access.errors.passwordNotChanged,
        );
      }
      if (error !== undefined) throw new Error(getMessages().access.errors.passwordNotChanged);
    },
  });
}

/**
 * Asking for a reset link. The answer never says whether the address belongs to an account, so
 * neither does this: success means "taken", not "sent".
 */
export function useForgotPassword() {
  return useMutation({
    mutationFn: async (input: { email: string }) => {
      const { error, response } = await api.POST("/api/auth/password/forgot", { body: input });
      if (response.status === 429) throw tooManyAttempts(response);
      if (response.status === 404) {
        throw new Error(getMessages().access.errors.resetUnavailable);
      }
      if (error !== undefined) throw new Error(getMessages().access.errors.forgotRequestFailed);
    },
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: async (input: { token: string; newPassword: string }) => {
      const { error, response } = await api.POST("/api/auth/password/reset", { body: input });
      if (response.status === 400) {
        throw new Error(
          typeof error === "string" ? error : getMessages().access.errors.passwordNotSet,
        );
      }
      if (error !== undefined) throw new Error(getMessages().access.errors.passwordNotSet);
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await api.POST("/api/auth/logout");
    },
    onSuccess: () => {
      queryClient.setQueryData(["auth", "me"], null);
      queryClient.invalidateQueries({ queryKey: ["auth"] });
    },
  });
}
