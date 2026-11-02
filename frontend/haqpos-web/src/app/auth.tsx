import { useCallback, useEffect, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { usersApi, type User } from "@/services/users";
import { AuthContext } from "@/app/auth-context";

export function AuthProvider({ children }: { children: ReactNode }) {
  const client = useQueryClient();
  const session = useQuery({
    queryKey: ["session"],
    queryFn: ({ signal }) => usersApi.me(signal),
    retry: false,
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
  const setUser = useCallback(
    (user: User | null) => {
      client.removeQueries({ predicate: (query) => query.queryKey[0] !== "session" });
      client.setQueryData(["session"], { user });
    },
    [client],
  );
  useEffect(() => {
    const expired = () => setUser(null);
    window.addEventListener("haqpos:unauthorized", expired);
    return () => window.removeEventListener("haqpos:unauthorized", expired);
  }, [setUser]);
  const refresh = async () => {
    await session.refetch();
  };
  const logout = async () => {
    await usersApi.logout();
    setUser(null);
  };
  return (
    <AuthContext.Provider
      value={{
        user: session.data?.user ?? null,
        loading: session.isPending,
        error: session.error,
        refresh,
        setUser,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
