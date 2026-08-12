import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/api/client";
import type { components } from "@/api/schema";

export type MachineDelegation = components["schemas"]["MachineDelegationDto"];
export type HeldMachineDelegation = components["schemas"]["HeldMachineDelegationDto"];
export type IssuedMachineDelegation = components["schemas"]["IssuedMachineDelegationDto"];
export type McpConnection = components["schemas"]["McpConnectionDto"];
export type MachineDelegationGrade = components["schemas"]["MachineDelegationGrade"];

export const DELEGATIONS_KEY = ["delegations"] as const;
export const HELD_DELEGATIONS_KEY = ["admin", "delegations"] as const;
export const MCP_CONNECTION_KEY = ["mcp", "connection"] as const;

/** Where the machine door answers, and whether it is open at all — everyone signed in may ask. */
export function useMcpConnection() {
  return useQuery({
    queryKey: MCP_CONNECTION_KEY,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/mcp/connection", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to read where the machine door answers.");
      }
      return data;
    },
  });
}

export function useOwnMachineDelegations() {
  return useQuery({
    queryKey: DELEGATIONS_KEY,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/machine-delegations", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to load your delegations.");
      }
      return data;
    },
  });
}

export function useIssueMachineDelegation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: {
      name: string;
      applicationId: string | null;
      lifetimeDays: number;
      grade: MachineDelegationGrade;
    }) => {
      const { data, error } = await api.POST("/api/machine-delegations", { body });
      if (error !== undefined || data === undefined) {
        throw new Error(typeof error === "string" ? error : "Failed to issue the delegation.");
      }
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: DELEGATIONS_KEY }),
  });
}

export function useRevokeMachineDelegation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { error } = await api.DELETE("/api/machine-delegations/{id}", { params: { path: { id } } });
      if (error !== undefined) {
        throw new Error("Failed to revoke the delegation.");
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: DELEGATIONS_KEY }),
  });
}

/** Every delegation on the server: what answering for the door requires of whoever opened it. */
export function useHeldMachineDelegations() {
  return useQuery({
    queryKey: HELD_DELEGATIONS_KEY,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/machine-delegations", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to load the delegations in issue.");
      }
      return data;
    },
  });
}

export function useRevokeHeldMachineDelegation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { error } = await api.DELETE("/api/admin/machine-delegations/{id}", {
        params: { path: { id } },
      });
      if (error !== undefined) {
        throw new Error("Failed to revoke the delegation.");
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: HELD_DELEGATIONS_KEY }),
  });
}

/** Where MCP is mounted. Bugler's own fact, not the operator's — see {@link doorAddress}. */
const DOOR_PATH = "/mcp";

/**
 * The address a tool is pointed at: the operator says where this Bugler is reachable, and Bugler
 * says where on itself the door is. Splitting it that way is what stops a command from depending on
 * whether whoever filled the settings in remembered to type a path — and an address that already
 * carries it is left alone, because somebody will type it anyway.
 */
export function doorAddress(publicUrl: string) {
  const trimmed = publicUrl.trim().replace(/\/+$/, "");
  return trimmed.endsWith(DOOR_PATH) ? trimmed : trimmed + DOOR_PATH;
}

/**
 * The command a person copies. Composed here rather than on the server because it is a fact about
 * their tool, not about Bugler — the server contributes only the address it answers at (ADR 0030).
 */
export function connectCommand(publicUrl: string, secret: string) {
  return [
    "claude mcp add --transport http bugler",
    doorAddress(publicUrl),
    `--header "Authorization: Bearer ${secret}"`,
  ].join(" ");
}
