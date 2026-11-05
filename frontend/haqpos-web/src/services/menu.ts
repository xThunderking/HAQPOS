import { getJson, sendJson } from "@/services/http-client";

export interface MenuProduct {
  id: string;
  areaId: string | null;
  code: string;
  name: string;
  category: string;
  description: string | null;
  price: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface MenuProductPage {
  items: MenuProduct[];
  total: number;
  page: number;
  pageSize: number;
}

export const menuApi = {
  list: (areaId: string, search: string, page: number, signal?: AbortSignal) =>
    getJson<MenuProductPage>(
      `menu/areas/${areaId}/products?${new URLSearchParams({ search, page: String(page) })}`,
      signal,
    ),
  unassigned: (page: number, signal?: AbortSignal) =>
    getJson<MenuProductPage>(`menu/unassigned?page=${page}`, signal),
  assignArea: (product: MenuProduct, areaId: string) =>
    sendJson<MenuProduct>(`menu/unassigned/${product.id}/area`, {
      areaId,
      version: product.version,
    }),
  create: (
    areaId: string,
    input: { name: string; category: string; description: string | null; price: number },
  ) => sendJson<MenuProduct>(`menu/areas/${areaId}/products`, input),
  update: (
    areaId: string,
    product: MenuProduct,
    input: {
      name: string;
      category: string;
      description: string | null;
      price: number;
    },
  ) =>
    sendJson<MenuProduct>(
      `menu/areas/${areaId}/products/${product.id}`,
      { ...input, version: product.version },
      "PUT",
    ),
  setActive: (areaId: string, product: MenuProduct, isActive: boolean) =>
    sendJson<MenuProduct>(`menu/areas/${areaId}/products/${product.id}/status`, {
      isActive,
      version: product.version,
    }),
};

export function menuErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "No fue posible completar la operación.";
}

export function formatPrice(value: number): string {
  return new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" }).format(value);
}
