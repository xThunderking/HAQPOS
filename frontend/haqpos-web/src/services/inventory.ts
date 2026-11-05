import { getJson, sendJson } from "@/services/http-client";

export type InventoryMovementType = "Entrada" | "Salida" | "Ajuste";

export interface InventoryItem {
  id: string;
  areaId: string;
  code: string;
  name: string;
  presentation: string | null;
  unit: string;
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
  inputQuantity: number | null;
  inputUnit: string | null;
}

export interface InventoryMovementPage {
  items: InventoryMovement[];
  total: number;
  page: number;
  pageSize: number;
}

export const inventoryApi = {
  list: (areaId: string, search: string, page: number, signal?: AbortSignal) =>
    getJson<InventoryPageResult>(
      `inventory/areas/${areaId}/items?${new URLSearchParams({ search, page: String(page) })}`,
      signal,
    ),
  findByBarcode: (areaId: string, barcode: string, signal?: AbortSignal) =>
    getJson<InventoryItem>(
      `inventory/areas/${areaId}/items/barcode?${new URLSearchParams({ barcode })}`,
      signal,
    ),
  create: (
    areaId: string,
    input: {
      code: string;
      name: string;
      presentation: string | null;
      unit: string;
      minimumStock: number;
      unitPrice: number;
      initialStock: number;
      generateBarcode: boolean;
    },
  ) => sendJson<InventoryItem>(`inventory/areas/${areaId}/items`, input),
  update: (
    areaId: string,
    item: InventoryItem,
    input: {
      code: string;
      name: string;
      presentation: string | null;
      minimumStock: number;
      unitPrice: number;
    },
  ) =>
    sendJson<InventoryItem>(
      `inventory/areas/${areaId}/items/${item.id}`,
      { ...input, version: item.version },
      "PUT",
    ),
  setActive: (areaId: string, item: InventoryItem, isActive: boolean) =>
    sendJson<InventoryItem>(`inventory/areas/${areaId}/items/${item.id}/status`, {
      isActive,
      version: item.version,
    }),
  move: (
    areaId: string,
    item: InventoryItem,
    type: InventoryMovementType,
    quantity: number,
    reason: string,
    unit: string = "Pieza",
  ) =>
    sendJson<InventoryItem>(`inventory/areas/${areaId}/items/${item.id}/movements`, {
      type,
      quantity,
      reason,
      unit,
    }),
  movements: (areaId: string, item: InventoryItem, page: number, signal?: AbortSignal) =>
    getJson<InventoryMovementPage>(
      `inventory/areas/${areaId}/items/${item.id}/movements?page=${page}`,
      signal,
    ),
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
