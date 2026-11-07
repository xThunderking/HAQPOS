import { getJson, sendJson } from "@/services/http-client";
import type { ClientRecord } from "@/services/clients";

export type PaymentMethod = "EFECTIVO" | "CXC" | "TARJETA" | "CORTESIA";
export type AccountStatus = "Abierta" | "Pagada" | "Pendiente" | "Cancelada";
export interface CashShift {
  id: string;
  number: number;
  areaId: string;
  areaName: string;
  cashierName: string;
  employeeNumber: string;
  openingAmount: number;
  openedAt: string;
  closedAt: string | null;
  openAccounts: number;
  paidAccounts: number;
  cancelledAccounts: number;
  cashTotal: number;
  cxcTotal: number;
  cardTotal: number;
  total: number;
  expectedCash: number;
  cashReceivedTotal: number;
  changeGivenTotal: number;
  pendingAccounts: number;
  courtesyAccounts: number;
}
export interface CashAccount {
  id: string;
  number: number;
  clientName: string;
  registrationNumber: string;
  status: AccountStatus;
  paymentMethod: PaymentMethod | null;
  total: number;
  openedAt: string;
  closedAt: string | null;
  version: string;
}
export interface CashLine {
  id: string;
  source: "Inventario" | "Menu";
  code: string;
  name: string;
  presentation: string | null;
  quantity: number;
  unitPrice: number;
  total: number;
  addedAt: string;
  removedAt: string | null;
  removalReason: string | null;
}
export interface CashAccountDetail {
  account: CashAccount;
  category: string;
  isExternal: boolean;
  office: string | null;
  cancellationReason: string | null;
  amountReceived: number | null;
  changeGiven: number | null;
  cardLastFour: string | null;
  cardAuthorization: string | null;
  cardBank: string | null;
  cardType: "CREDITO" | "DEBITO" | null;
  lines: CashLine[];
}
export interface CashWorkspace {
  canOperate: boolean;
  assignedAreaId: string | null;
  assignedAreaName: string | null;
  shift: CashShift | null;
  accounts: CashAccount[];
}
export interface CashProduct {
  id: string;
  source: "Inventario" | "Menu";
  code: string;
  name: string;
  presentation: string | null;
  price: number;
  stock: number | null;
}
export interface CashHistory {
  items: CashShift[];
  total: number;
  page: number;
  pageSize: number;
}
export interface CashCut {
  shift: CashShift;
  accounts: CashAccountDetail[];
}
export interface CashReceivable {
  account: CashAccount;
  shiftNumber: number;
  areaName: string;
  cashierName: string;
  employeeNumber: string;
  category: string;
  isExternal: boolean;
  office: string | null;
  lines: CashLine[];
}
export interface CashReceivablesPage {
  items: CashReceivable[];
  total: number;
  page: number;
  pageSize: number;
  totalAmount: number;
}
export const cashApi = {
  workspace: (signal?: AbortSignal) => getJson<CashWorkspace>("cash/workspace", signal),
  history: (page: number, signal?: AbortSignal) =>
    getJson<CashHistory>(`cash/shifts?page=${page}`, signal),
  receivables: (page: number, search: string, signal?: AbortSignal) =>
    getJson<CashReceivablesPage>(
      `cash/receivables?${new URLSearchParams({ page: String(page), search })}`,
      signal,
    ),
  openShift: (requestId: string, areaId: string, openingAmount: number) =>
    sendJson<CashShift>("cash/shifts", { requestId, areaId, openingAmount }),
  closeShift: (id: string) => sendJson<CashShift>(`cash/shifts/${id}/close`),
  openAccount: (shiftId: string, requestId: string, clientId: string) =>
    sendJson<CashAccountDetail>(`cash/shifts/${shiftId}/accounts`, { requestId, clientId }),
  createClient: (input: {
    registrationNumber: string;
    fullName: string;
    isExternal: boolean;
    office: string | null;
  }) => sendJson<ClientRecord>("cash/clients", input),
  account: (id: string, signal?: AbortSignal) =>
    getJson<CashAccountDetail>(`cash/accounts/${id}`, signal),
  products: (shiftId: string, search: string, signal?: AbortSignal) =>
    getJson<CashProduct[]>(
      `cash/shifts/${shiftId}/products?${new URLSearchParams({ search })}`,
      signal,
    ),
  add: (accountId: string, requestId: string, product: CashProduct, quantity: number) =>
    sendJson<CashAccountDetail>(`cash/accounts/${accountId}/lines`, {
      requestId,
      source: product.source,
      productId: product.id,
      quantity,
    }),
  remove: (accountId: string, lineId: string, reason: string) =>
    sendJson<CashAccountDetail>(`cash/accounts/${accountId}/lines/${lineId}/remove`, { reason }),
  pay: (
    account: CashAccount,
    paymentMethod: PaymentMethod,
    details: {
      amountReceived?: number;
      cardLastFour?: string;
      cardAuthorization?: string;
      cardBank?: string;
      cardType?: "CREDITO" | "DEBITO";
    } = {},
  ) =>
    sendJson<CashAccountDetail>(`cash/accounts/${account.id}/pay`, {
      version: account.version,
      paymentMethod,
      ...details,
    }),
  cancel: (account: CashAccount, reason: string) =>
    sendJson<CashAccountDetail>(`cash/accounts/${account.id}/cancel`, {
      version: account.version,
      reason,
    }),
  cut: (id: string, signal?: AbortSignal) => getJson<CashCut>(`cash/shifts/${id}/cut`, signal),
  receiptUrl: (id: string, download = false) =>
    `${(import.meta.env.VITE_API_BASE_URL || "/api").replace(/\/$/, "")}/cash/shifts/${id}/receipt?download=${download}`,
};
export const money = (value: number) =>
  new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" }).format(value);
export const cashError = (error: unknown) =>
  error instanceof Error ? error.message : "No fue posible completar la operación de caja.";
export const folio = (value: number) => String(value).padStart(6, "0");
