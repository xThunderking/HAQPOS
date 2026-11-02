import { createContext } from "react";
import type { User } from "@/services/users";

export interface AuthContextValue {
  user: User | null;
  loading: boolean;
  error: Error | null;
  refresh: () => Promise<void>;
  setUser: (user: User | null) => void;
  logout: () => Promise<void>;
}
export const AuthContext = createContext<AuthContextValue | null>(null);
