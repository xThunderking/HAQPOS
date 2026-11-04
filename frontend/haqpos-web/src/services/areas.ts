import { getJson, sendJson } from "@/services/http-client";

export interface AreaRecord {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  version: string;
}

export interface AreaPageResult {
  items: AreaRecord[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AreaOption {
  id: string;
  name: string;
}

export const areasApi = {
  list: (search: string, page: number, signal?: AbortSignal) =>
    getJson<AreaPageResult>(`areas?${new URLSearchParams({ search, page: String(page) })}`, signal),
  options: (signal?: AbortSignal) => getJson<AreaOption[]>("areas/options", signal),
  create: (input: { name: string; description: string }) => sendJson<AreaRecord>("areas", input),
};

export function areaErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "No fue posible registrar el área.";
}
