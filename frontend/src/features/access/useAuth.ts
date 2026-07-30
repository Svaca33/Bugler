import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/api/client";

export function useCurrentUser() {
  return useQuery({
    queryKey: ["auth", "me"],
    queryFn: async () => {
      const { data, response } = await api.GET("/api/auth/me");
      if (response.status === 401) return null;
      if (data === undefined) throw new Error("Failed to load session");
      return data;
    },
    retry: false,
    staleTime: 60_000,
  });
}

export function useAuthStatus() {
  return useQuery({
    queryKey: ["auth", "status"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/auth/status");
      if (error !== undefined) throw new Error("Failed to load auth status");
      return data;
    },
    retry: false,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (credentials: { email: string; password: string; staySignedIn: boolean }) => {
      const { data, response } = await api.POST("/api/auth/login", { body: credentials });
      if (response.status === 401) throw new Error("Invalid e-mail or password.");
      if (data === undefined) throw new Error("Login failed.");
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
      if (response.status === 409) throw new Error("Setup has already been completed.");
      if (data === undefined) throw new Error("Setup failed — check the e-mail and password (min 8 chars).");
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
        throw new Error(typeof error === "string" ? error : "The password was not changed.");
      }
      if (error !== undefined) throw new Error("The password was not changed.");
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
