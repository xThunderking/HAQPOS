import { getJson, sendJson } from "@/services/http-client";

export type UserRole = "Administrador" | "Supervisor" | "Cajero";
export const roles: UserRole[] = ["Administrador", "Supervisor", "Cajero"];
export const roleDescriptions: Record<UserRole, string> = {
  Administrador: "Control total del sistema y de las cuentas de usuario.",
  Supervisor: "Acceso de supervisión. Sin administración de usuarios.",
  Cajero: "Acceso operativo de caja. Sin administración de usuarios.",
};
export interface User {
  id: string;
  employeeNumber: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  lockedUntil: string | null;
  temporaryPasswordExpiresAt: string | null;
  version: string;
}
export interface TemporaryCredentials {
  user: User;
  temporaryPassword: string;
  expiresAt: string;
}
export interface UserPage {
  items: User[];
  total: number;
  page: number;
  pageSize: number;
}
export interface AuditEvent {
  id: number;
  occurredAt: string;
  action: string;
  actorEmployeeNumber: string | null;
  subjectEmployeeNumber: string | null;
}
export interface AuditPage {
  items: AuditEvent[];
  total: number;
  page: number;
  pageSize: number;
}

export const usersApi = {
  me: (signal?: AbortSignal) => getJson<{ user: User | null }>("auth/me", signal),
  login: (employeeNumber: string, password: string) =>
    sendJson<{ user: User }>("auth/login", { employeeNumber, password }, "POST", false),
  logout: () => sendJson<void>("auth/logout"),
  changePassword: (currentPassword: string, newPassword: string, confirmPassword: string) =>
    sendJson<{ user: User }>(
      "auth/change-password",
      { currentPassword, newPassword, confirmPassword },
      "POST",
      false,
    ),
  list: (search: string, page: number, signal?: AbortSignal) =>
    getJson<UserPage>(`users?${new URLSearchParams({ search, page: String(page) })}`, signal),
  create: (employeeNumber: string, fullName: string, role: UserRole) =>
    sendJson<TemporaryCredentials>("users", { employeeNumber, fullName, role }),
  update: (user: User, fullName: string, role: UserRole, isActive: boolean) =>
    sendJson<User>(`users/${user.id}`, { fullName, role, isActive, version: user.version }, "PUT"),
  unlock: (id: string) => sendJson<User>(`users/${id}/unlock`),
  reset: (id: string) => sendJson<TemporaryCredentials>(`users/${id}/reset-password`),
  audit: (page: number, signal?: AbortSignal) =>
    getJson<AuditPage>(`security/audit?page=${page}`, signal),
};

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "No fue posible completar la operación.";
}
export function formatDate(value: string | null): string {
  return value
    ? new Intl.DateTimeFormat("es-MX", { dateStyle: "medium", timeStyle: "short" }).format(
        new Date(value),
      )
    : "Sin ingresos";
}
