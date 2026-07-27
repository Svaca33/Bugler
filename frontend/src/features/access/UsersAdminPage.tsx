import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/** Admin management of people: accounts and their per-application read grants. */
export function UsersAdminPage() {
  const queryClient = useQueryClient();
  const catalog = useCatalog();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isAdmin, setIsAdmin] = useState(false);

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

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Users</CardTitle>
      </CardHeader>
      <CardContent className="grid gap-4">
        <table className="w-full text-sm">
          <thead className="text-left text-muted-foreground">
            <tr>
              <th className="py-1 font-medium">E-mail</th>
              <th className="py-1 font-medium">Role</th>
              <th className="py-1 font-medium">Application access</th>
              <th className="py-1 font-medium text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {(users.data ?? []).map(user => (
              <tr key={user.id} className={`border-t ${user.isDeactivated ? "opacity-50" : ""}`}>
                <td className="py-1.5">{user.email}</td>
                <td className="py-1.5">{user.isAdmin ? "Admin" : "Member"}</td>
                <td className="py-1.5">
                  {user.isAdmin ? (
                    <span className="text-muted-foreground">everything</span>
                  ) : (
                    <div className="flex flex-wrap gap-2">
                      {applications.map(app => {
                        const granted = user.grantedApplicationIds.includes(app.id);
                        return (
                          <label key={app.id} className="flex items-center gap-1 text-xs">
                            <input
                              type="checkbox"
                              checked={granted}
                              onChange={() =>
                                (granted ? revoke : grant).mutate({
                                  userId: user.id,
                                  applicationId: app.id,
                                })
                              }
                            />
                            {app.name}
                          </label>
                        );
                      })}
                    </div>
                  )}
                </td>
                <td className="py-1.5 text-right">
                  {!user.isDeactivated && (
                    <Button variant="outline" size="sm" onClick={() => deactivate.mutate(user.id)}>
                      Deactivate
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={event => {
            event.preventDefault();
            createUser.mutate();
          }}
        >
          <div className="grid gap-1">
            <Label htmlFor="new-user-email">E-mail</Label>
            <Input
              id="new-user-email"
              type="email"
              className="w-56"
              value={email}
              onChange={event => setEmail(event.target.value)}
              required
            />
          </div>
          <div className="grid gap-1">
            <Label htmlFor="new-user-password">Password</Label>
            <Input
              id="new-user-password"
              type="password"
              className="w-44"
              value={password}
              onChange={event => setPassword(event.target.value)}
              required
            />
          </div>
          <label className="mb-2 flex items-center gap-1.5 text-sm">
            <input type="checkbox" checked={isAdmin} onChange={() => setIsAdmin(!isAdmin)} />
            Admin
          </label>
          <Button type="submit" size="sm" disabled={createUser.isPending}>
            Create user
          </Button>
          {createUser.error !== null && (
            <p className="text-sm text-destructive">{createUser.error.message}</p>
          )}
        </form>
      </CardContent>
    </Card>
  );
}
