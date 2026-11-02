import { useContext } from "react";
import { AuthContext } from "@/app/auth-context";

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("Falta el proveedor de autenticación.");
  return context;
}
