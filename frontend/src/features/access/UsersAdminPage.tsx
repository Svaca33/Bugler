import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

import { useCurrentUser } from "./useAuth";

const CAPTION = "font-mono text-[10px] tracking-[0.12em] text-[#5F7590]";

/** Admin management of people: accounts and their per-application read grants. */
export function UsersAdminPage() {
  const queryClient = useQueryClient();
  const catalog = useCatalog();
  const me = useCurrentUser();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isAdmin, setIsAdmin] = useState(false);
  const [userFilter, setUserFilter] = useState("");

  const users = useQuery({
    queryKey: ["admin", "users"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/users");
      if (error !== undefined) throw new Error("Failed to load users");
      return data;
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin", "users"] });

  const createUser = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/users", {
        body: { email, password, displayName: null, isAdmin },
      });
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to create user (password must have at least 8 characters).");
      }
      return data;
    },
    onSuccess: () => {
      setEmail("");
      setPassword("");
      setIsAdmin(false);
      invalidate();
    },
  });

  const grant = useMutation({
    mutationFn: async (input: { userId: string; applicationId: string }) => {
      await api.POST("/api/users/{id}/grants", {
        params: { path: { id: input.userId } },
        body: { applicationId: input.applicationId },
      });
    },
    onSuccess: invalidate,
  });

  const revoke = useMutation({
    mutationFn: async (input: { userId: string; applicationId: string }) => {
      await api.DELETE("/api/users/{id}/grants/{applicationId}", {
        params: { path: { id: input.userId, applicationId: input.applicationId } },
      });
    },
    onSuccess: invalidate,
  });

  const deactivate = useMutation({
    mutationFn: async (userId: string) => {
      await api.POST("/api/users/{id}/deactivate", { params: { path: { id: userId } } });
    },
    onSuccess: invalidate,
  });

  const applications = catalog.data?.applications ?? [];
  const allUsers = users.data ?? [];
  const visibleUsers = allUsers.filter(user =>
    user.email.toLowerCase().includes(userFilter.trim().toLowerCase()),
  );
  const deactivatedCount = allUsers.filter(user => user.isDeactivated).length;

  // One column per application, generated from the catalog length.
  const gridTemplateColumns = `250px 92px 104px repeat(${Math.max(applications.length, 1)}, minmax(0,1fr)) 104px`;

  return (
    <div className="flex h-full min-h-0 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex items-center gap-3">
        <h2 className="text-[17px] font-semibold tracking-[-0.3px]">Users</h2>
        <span className="text-[12.5px] text-[#8CA1B8]">
          {allUsers.length} {allUsers.length === 1 ? "account" : "accounts"}
          {deactivatedCount > 0 && ` · ${deactivatedCount} deactivated`}
        </span>
        <Input
          className="ml-auto w-[232px]"
          placeholder="Filter by e-mail…"
          value={userFilter}
          onChange={event => setUserFilter(event.target.value)}
        />
      </div>

      <div className="overflow-x-auto rounded-[11px] border border-[#1E344C] bg-card">
        <div
          className="grid items-end gap-3 border-b border-[#1E344C] bg-[#0B1826] px-[18px] py-3"
          style={{ gridTemplateColumns }}
        >
          <span className={CAPTION}>E-MAIL</span>
          <span className={CAPTION}>ROLE</span>
          <span className={CAPTION}>STATUS</span>
          {applications.length > 0 ? (
            applications.map(app => (
              <span
                key={app.id}
                className="break-words text-center font-mono text-[10.5px] leading-[1.35] text-[#8CA1B8]"
              >
                {app.name}
              </span>
            ))
          ) : (
            <span className="text-center font-mono text-[10.5px] text-[#5F7590]">—</span>
          )}
          <span className={`${CAPTION} text-right`}>ACTIONS</span>
        </div>

        {visibleUsers.map(user => {
          const isSelf = user.id === me.data?.id;
          return (
            <div
              key={user.id}
              className={`grid items-center gap-3 border-b border-[#17293D] px-[18px] py-[11px] last:border-b-0 ${
                user.isDeactivated ? "bg-sidebar" : "hover:bg-[#12243A]"
              }`}
              style={{ gridTemplateColumns }}
            >
              <span
                className={`truncate font-mono text-xs ${user.isDeactivated ? "text-[#7D93AA]" : "text-[#DCE8F3]"}`}
                title={user.email}
              >
                {user.email}
              </span>
              <span
                className={`w-fit rounded-[5px] px-2 py-0.5 text-[11.5px] ${
                  user.isAdmin
                    ? "bg-[rgba(233,164,60,0.14)] text-[#F6C170]"
                    : "bg-[#16283C] text-[#B6C8DA]"
                } ${user.isDeactivated ? "opacity-60" : ""}`}
              >
                {user.isAdmin ? "Admin" : "Member"}
              </span>
              {user.isDeactivated ? (
                <span className="w-fit rounded-[5px] border border-[#22394F] px-2 py-0.5 text-[11px] text-[#7D93AA]">
                  Deactivated
                </span>
              ) : (
                <span className="text-[11.5px] text-[#8CA1B8]">{isSelf ? "Active · you" : "Active"}</span>
              )}

              {user.isDeactivated ? (
                <span
                  className="text-center text-xs text-[#5F7590]"
                  style={{ gridColumn: `span ${Math.max(applications.length, 1)}` }}
                >
                  grants retained, no access while deactivated
                </span>
              ) : user.isAdmin ? (
                <span
                  className="rounded-md bg-[#12253A] text-center text-xs leading-[26px] text-[#8CA1B8]"
                  style={{ gridColumn: `span ${Math.max(applications.length, 1)}` }}
                >
                  every application — admins are never scoped
                </span>
              ) : applications.length > 0 ? (
                applications.map(app => {
                  const granted = user.grantedApplicationIds.includes(app.id);
                  return (
                    <span key={app.id} className="grid place-items-center">
                      <input
                        type="checkbox"
                        className="size-3.5"
                        style={{ accentColor: "var(--primary)" }}
                        aria-label={`${user.email} may read ${app.name}`}
                        checked={granted}
                        onChange={() =>
                          (granted ? revoke : grant).mutate({
                            userId: user.id,
                            applicationId: app.id,
                          })
                        }
                      />
                    </span>
                  );
                })
              ) : (
                <span />
              )}

              <span className="justify-self-end">
                {!user.isDeactivated && (
                  <button
                    type="button"
                    className="rounded-[5px] px-2 py-1 text-[11.5px] text-[#F0685A] hover:bg-[rgba(229,84,74,0.14)]"
                    onClick={() => deactivate.mutate(user.id)}
                  >
                    Deactivate
                  </button>
                )}
              </span>
            </div>
          );
        })}
        {visibleUsers.length === 0 && (
          <p className="px-[18px] py-8 text-center text-sm text-[#8CA1B8]">
            No accounts match the filter.
          </p>
        )}
      </div>

      <form
        className="flex flex-col gap-3 rounded-[11px] border border-dashed border-input p-4"
        onSubmit={event => {
          event.preventDefault();
          createUser.mutate();
        }}
      >
        <div className="flex items-baseline gap-3">
          <span className={CAPTION}>CREATE USER</span>
          <span className="text-[11.5px] text-[#7D93AA]">
            Grants are assigned in the matrix above once the account exists.
          </span>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <div className="grid gap-1.5">
            <Label htmlFor="new-user-email">E-mail</Label>
            <Input
              id="new-user-email"
              type="email"
              className="w-[248px]"
              placeholder="name@company.com"
              value={email}
              onChange={event => setEmail(event.target.value)}
              required
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="new-user-password">Password (min 8 characters)</Label>
            <Input
              id="new-user-password"
              type="password"
              className="w-[200px]"
              value={password}
              onChange={event => setPassword(event.target.value)}
              required
            />
          </div>
          <label className="flex h-9 items-center gap-1.5 text-[12.5px] text-[#C6D6E6]">
            <input
              type="checkbox"
              className="size-3.5"
              style={{ accentColor: "var(--primary)" }}
              checked={isAdmin}
              onChange={() => setIsAdmin(!isAdmin)}
            />
            Server administrator
          </label>
          <Button type="submit" size="sm" disabled={createUser.isPending}>
            Create user
          </Button>
        </div>
        {createUser.error !== null && (
          <p className="text-[12.5px] text-[#F0685A]">{createUser.error.message}</p>
        )}
      </form>
    </div>
  );
}
