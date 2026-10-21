import { getJson } from "@/services/http-client";

export interface HealthResponse {
  status: string;
}

export function getHealth({ signal }: { signal: AbortSignal }) {
  return getJson<HealthResponse>("/health", signal);
}
