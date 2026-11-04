import { getJson, sendJson } from "@/services/http-client";

export interface ClientRecord {
  id: string;
  registrationNumber: string;
  fullName: string;
  category: "NORMAL" | "CORTESIA" | "CONVENIO";
  isExternal: boolean;
  office: string | null;
  createdAt: string;
  version: string;
}

export interface ClientPageResult {
  items: ClientRecord[];
  total: number;
  page: number;
  pageSize: number;
}

export const clientsApi = {
  list: (search: string, page: number, signal?: AbortSignal) =>
    getJson<ClientPageResult>(
      `clients?${new URLSearchParams({ search, page: String(page) })}`,
      signal,
    ),
  create: (input: {
    registrationNumber: string;
    fullName: string;
    category: "NORMAL" | "CORTESIA" | "CONVENIO";
    isExternal: boolean;
    office: string | null;
  }) => sendJson<ClientRecord>("clients", input),
  update: (
    id: string,
    input: {
      registrationNumber: string;
      fullName: string;
      category: "NORMAL" | "CORTESIA" | "CONVENIO";
      isExternal: boolean;
      office: string | null;
      version: string;
    },
  ) => sendJson<ClientRecord>(`clients/${id}`, input, "PUT"),
  delete: (id: string, version: string) => sendJson<void>(`clients/${id}/delete`, { version }),
};

export function clientErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "No fue posible registrar al cliente.";
}
