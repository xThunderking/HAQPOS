import { getJson, sendJson } from "@/services/http-client";

export interface MenuProduct {
  id: string;
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
  list: (search: string, page: number, signal?: AbortSignal) =>
    getJson<MenuProductPage>(
      `menu/products?${new URLSearchParams({ search, page: String(page) })}`,
      signal,
    ),
  create: (input: {
    code: string;
    name: string;
    category: string;
    description: string | null;
    price: number;
  }) => sendJson<MenuProduct>("menu/products", input),
  update: (
    product: MenuProduct,
    input: {
      code: string;
      name: string;
      category: string;
      description: string | null;
      price: number;
    },
  ) =>
    sendJson<MenuProduct>(
      `menu/products/${product.id}`,
      { ...input, version: product.version },
      "PUT",
    ),
  setActive: (product: MenuProduct, isActive: boolean) =>
    sendJson<MenuProduct>(`menu/products/${product.id}/status`, {
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
