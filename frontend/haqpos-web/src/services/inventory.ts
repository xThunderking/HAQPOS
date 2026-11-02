import { getJson, sendJson } from "@/services/http-client";

export type InventoryMovementType = "Entrada" | "Salida" | "Ajuste";

export interface InventoryItem {
  id: string;
  code: string;
  name: string;
  category: string;
  unit: string;
  location: string | null;
  currentStock: number;
  minimumStock: number;
  unitPrice: number | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface InventoryPageResult {
  items: InventoryItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface InventoryMovement {
  id: string;
  type: InventoryMovementType;
  quantity: number;
  stockBefore: number;
  stockAfter: number;
  reason: string;
  occurredAt: string;
  actorEmployeeNumber: string;
  actorFullName: string;
}

export interface InventoryMovementPage {
  items: InventoryMovement[];
  total: number;
  page: number;
  pageSize: number;
}

export const inventoryApi = {
  list: (search: string, page: number, signal?: AbortSignal) =>
    getJson<InventoryPageResult>(
      `inventory/items?${new URLSearchParams({ search, page: String(page) })}`,
      signal,
    ),
  create: (input: {
    code: string;
    name: string;
    category: string;
    unit: string;
    location: string | null;
    minimumStock: number;
    unitPrice: number;
    initialStock: number;
  }) => sendJson<InventoryItem>("inventory/items", input),
  update: (
    item: InventoryItem,
    input: {
      code: string;
      name: string;
      category: string;
      unit: string;
      location: string | null;
      minimumStock: number;
      unitPrice: number;
    },
  ) =>
    sendJson<InventoryItem>(
      `inventory/items/${item.id}`,
      { ...input, version: item.version },
      "PUT",
    ),
  setActive: (item: InventoryItem, isActive: boolean) =>
    sendJson<InventoryItem>(`inventory/items/${item.id}/status`, {
      isActive,
      version: item.version,
    }),
  move: (item: InventoryItem, type: InventoryMovementType, quantity: number, reason: string) =>
    sendJson<InventoryItem>(`inventory/items/${item.id}/movements`, { type, quantity, reason }),
  movements: (item: InventoryItem, page: number, signal?: AbortSignal) =>
    getJson<InventoryMovementPage>(`inventory/items/${item.id}/movements?page=${page}`, signal),
};

export function inventoryErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "No fue posible completar la operación.";
}

export function formatQuantity(value: number): string {
  return new Intl.NumberFormat("es-MX", { maximumFractionDigits: 3 }).format(value);
}

export function formatUnitPrice(value: number | null): string {
  return value === null
    ? "Sin precio"
    : new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" }).format(value);
}
