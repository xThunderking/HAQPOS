import { useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Banknote,
  Check,
  ChevronRight,
  CreditCard,
  Download,
  Plus,
  Printer,
  ReceiptText,
  Search,
  UserRound,
  X,
} from "lucide-react";
import { Modal, Notice } from "@/components/user-interface";
import { useCashCommandId, useDebouncedSearch } from "@/hooks/use-cash-command";
import { clientsApi, type ClientRecord } from "@/services/clients";
import { formatDate } from "@/services/users";
import {
  cashApi,
  cashError,
  folio,
  money,
  type CashAccountDetail,
  type CashLine,
  type CashShift,
  type PaymentMethod,
} from "@/services/cash";

export function ReceiptButtons({ shiftId }: { shiftId: string }) {
  return (
    <div className="cash-receipt-actions">
      <a
        className="button button-secondary button-small"
        href={cashApi.receiptUrl(shiftId)}
        target="_blank"
        rel="noopener noreferrer"
      >
        <Printer size={16} /> Ver / imprimir
      </a>
      <a className="button button-primary button-small" href={cashApi.receiptUrl(shiftId, true)}>
        <Download size={16} /> Descargar PDF
      </a>
    </div>
  );
}

export function NewAccountDialog({
  shift,
  onClose,
  onSaved,
}: {
  shift: CashShift;
  onClose: () => void;
  onSaved: (id: string) => void;
}) {
  const [search, setSearch] = useState("");
  const debounced = useDebouncedSearch(search);
  const [selected, setSelected] = useState<ClientRecord | null>(null);
  const [creating, setCreating] = useState(false);
  const [registrationNumber, setRegistration] = useState("");
  const [fullName, setFullName] = useState("");
  const [isExternal, setExternal] = useState(false);
  const [office, setOffice] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const command = useCashCommandId();
  const query = useQuery({
    queryKey: ["cash-clients", debounced],
    queryFn: ({ signal }) => clientsApi.list(debounced, 1, signal),
    enabled: !creating && !selected && debounced.length > 0,
  });
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      let customer = selected;
      if (!customer && creating) {
        customer = await cashApi.createClient({
          registrationNumber,
          fullName: fullName.trim(),
          isExternal,
          office: isExternal ? null : office.trim(),
        });
        setSelected(customer);
        setCreating(false);
      }
      if (customer) {
        const result = await cashApi.openAccount(
          shift.id,
          command.for(`${shift.id}:${customer.id}`),
          customer.id,
        );
        command.clear();
        onSaved(result.account.id);
      }
    } catch (failure) {
      setError(cashError(failure));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title="Abrir cuenta"
      description={`Área ${shift.areaName} · Selecciona al cliente de la cuenta.`}
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        {selected ? (
          <div className="cash-selected-client">
            <UserRound size={24} />
            <div>
              <strong>{selected.fullName}</strong>
              <span>
                {selected.registrationNumber} ·{" "}
                {selected.isExternal ? "Externo" : `Consultorio ${selected.office}`}
              </span>
            </div>
            <button
              type="button"
              className="icon-button"
              disabled={busy}
              aria-label="Cambiar cliente"
              onClick={() => setSelected(null)}
            >
              <X size={17} />
            </button>
          </div>
        ) : creating ? (
          <>
            <div className="cash-form-heading">
              <strong>Nuevo cliente</strong>
              <span className="status status-neutral">NORMAL</span>
            </div>
            <label className="field">
              <span>Número GASS / No. colaborador</span>
              <input
                autoFocus
                required
                maxLength={10}
                pattern="[A-Za-z0-9]{1,10}"
                value={registrationNumber}
                onChange={(e) =>
                  setRegistration(e.target.value.replace(/[^a-zA-Z0-9]/g, "").toUpperCase())
                }
              />
            </label>
            <label className="field">
              <span>Nombre completo</span>
              <input
                required
                minLength={3}
                maxLength={150}
                value={fullName}
                onChange={(e) => setFullName(e.target.value.replace(/\p{N}/gu, ""))}
                autoComplete="name"
              />
            </label>
            <label className="cash-check">
              <input
                type="checkbox"
                checked={isExternal}
                onChange={(e) => setExternal(e.target.checked)}
              />{" "}
              Externo
            </label>
            {!isExternal && (
              <label className="field">
                <span>Consultorio</span>
                <input
                  required
                  minLength={2}
                  maxLength={80}
                  value={office}
                  onChange={(e) => setOffice(e.target.value)}
                />
              </label>
            )}
          </>
        ) : (
          <>
            <label className="field">
              <span>Buscar cliente</span>
              <div className="search-input">
                <Search size={18} />
                <input
                  autoFocus
                  value={search}
                  maxLength={100}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Nombre, número GASS o de colaborador"
                />
              </div>
            </label>
            <div className="cash-client-results">
              {!debounced ? (
                <p>Escribe el nombre o el identificador del cliente.</p>
              ) : query.isPending ? (
                <p role="status">Buscando clientes…</p>
              ) : query.isError ? (
                <Notice>{cashError(query.error)}</Notice>
              ) : query.data.items.length === 0 ? (
                <p>No encontramos un cliente con esos datos.</p>
              ) : (
                query.data.items.map((customer) => (
                  <button
                    type="button"
                    className="cash-client-option"
                    key={customer.id}
                    onClick={() => setSelected(customer)}
                  >
                    <span>
                      <strong>{customer.fullName}</strong>
                      <small>
                        {customer.registrationNumber} ·{" "}
                        {customer.isExternal ? "Externo" : customer.office}
                      </small>
                    </span>
                    <ChevronRight size={17} />
                  </button>
                ))
              )}
              {query.data && query.data.total > 20 && (
                <small>Hay más coincidencias. Escribe más datos para precisar la búsqueda.</small>
              )}
            </div>
            <button
              type="button"
              className="button button-secondary"
              onClick={() => {
                setCreating(true);
                setError("");
              }}
            >
              <Plus size={17} /> Registrar nuevo cliente
            </button>
          </>
        )}
        <div className="modal-actions">
          <button
            type="button"
            className="button button-secondary"
            disabled={busy}
            onClick={creating ? () => setCreating(false) : onClose}
          >
            {creating ? "Volver a buscar" : "Cancelar"}
          </button>
          <button className="button button-primary" disabled={busy || (!selected && !creating)}>
            {busy ? "Abriendo…" : creating ? "Registrar y abrir cuenta" : "Abrir cuenta"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

export function AccountActionDialog({
  detail,
  action,
  line,
  onClose,
  onSaved,
}: {
  detail: CashAccountDetail;
  action: "pay" | "remove" | "cancel";
  line?: CashLine;
  onClose: () => void;
  onSaved: () => void;
}) {
  const client = useQueryClient();
  const [method, setMethod] = useState<PaymentMethod | null>(
    detail.category === "CORTESIA" ? "CORTESIA" : null,
  );
  const [amountReceived, setAmountReceived] = useState("");
  const [cardLastFour, setCardLastFour] = useState("");
  const [cardAuthorization, setCardAuthorization] = useState("");
  const [cardBank, setCardBank] = useState("");
  const [cardType, setCardType] = useState<"CREDITO" | "DEBITO" | "">("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      if (action === "pay") {
        if (!method) return;
        const paymentDetails =
          method === "EFECTIVO"
            ? { amountReceived: Number(amountReceived) }
            : method === "TARJETA"
              ? {
                  cardLastFour,
                  cardAuthorization,
                  cardBank,
                  cardType: cardType as "CREDITO" | "DEBITO",
                }
              : {};
        await cashApi.pay(detail.account, method, paymentDetails);
      } else if (action === "remove" && line)
        await cashApi.remove(detail.account.id, line.id, reason.trim());
      else await cashApi.cancel(detail.account, reason.trim());
      onSaved();
    } catch (failure) {
      setError(cashError(failure));
      void client.invalidateQueries({ queryKey: ["cash-account", detail.account.id] });
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title={
        action === "pay"
          ? detail.category === "CORTESIA"
            ? "Cerrar cuenta de cortesía"
            : "Cobrar y cerrar cuenta"
          : action === "remove"
            ? "Retirar producto"
            : "Cancelar cuenta"
      }
      description={`Cuenta #${folio(detail.account.number)} · ${detail.account.clientName}`}
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(e) => void submit(e)}>
        {error && <Notice>{error}</Notice>}
        {action === "pay" ? (
          <>
            <div className="cash-payment-total">
              <span>
                {detail.category === "CORTESIA" ? "Total de cortesía" : "Total de la cuenta"}
              </span>
              <strong>{money(detail.account.total)}</strong>
            </div>
            {detail.category === "CORTESIA" ? (
              <Notice success>
                Este cliente tiene categoría CORTESIA. Se conservarán los productos consumidos y el
                inventario descontado; el total a pagar es $0.00.
              </Notice>
            ) : (
              <>
                <fieldset className="cash-payment-options">
                  <legend>Forma de pago</legend>
                  {(
                    [
                      ["EFECTIVO", Banknote],
                      ["CXC", ReceiptText],
                      ["TARJETA", CreditCard],
                    ] as const
                  ).map(([value, Icon]) => (
                    <label key={value} data-selected={method === value}>
                      <input
                        type="radio"
                        name="payment"
                        value={value}
                        checked={method === value}
                        onChange={() => setMethod(value)}
                        required
                      />
                      <Icon size={23} />
                      <strong>{value}</strong>
                      {method === value && <Check size={15} />}
                    </label>
                  ))}
                </fieldset>
                {method === "EFECTIVO" && (
                  <>
                    <label className="field">
                      <span>Cantidad recibida (MXN)</span>
                      <input
                        autoFocus
                        type="number"
                        inputMode="decimal"
                        min={detail.account.total}
                        max="999999999.99"
                        step="0.01"
                        required
                        value={amountReceived}
                        onChange={(event) => setAmountReceived(event.target.value)}
                      />
                    </label>
                    <div className="cash-change-due" aria-live="polite">
                      <span>Cambio a entregar</span>
                      <strong>
                        {money(Math.max(0, (Number(amountReceived) || 0) - detail.account.total))}
                      </strong>
                    </div>
                  </>
                )}
                {method === "TARJETA" && (
                  <div className="cash-card-fields">
                    <label className="field">
                      <span>Últimos 4 dígitos de la tarjeta</span>
                      <input
                        autoFocus
                        inputMode="numeric"
                        autoComplete="off"
                        maxLength={4}
                        pattern="[0-9]{4}"
                        required
                        value={cardLastFour}
                        onChange={(event) => setCardLastFour(event.target.value.replace(/\D/g, ""))}
                      />
                      <small>No se almacena el número completo ni el CVV.</small>
                    </label>
                    <label className="field">
                      <span>Número de autorización</span>
                      <input
                        required
                        maxLength={40}
                        autoComplete="off"
                        value={cardAuthorization}
                        onChange={(e) => setCardAuthorization(e.target.value)}
                      />
                    </label>
                    <label className="field">
                      <span>Banco</span>
                      <input
                        required
                        maxLength={80}
                        value={cardBank}
                        onChange={(e) => setCardBank(e.target.value)}
                      />
                    </label>
                    <label className="field">
                      <span>Tipo</span>
                      <select
                        required
                        value={cardType}
                        onChange={(e) => setCardType(e.target.value as "CREDITO" | "DEBITO" | "")}
                      >
                        <option value="">Selecciona</option>
                        <option value="CREDITO">Crédito</option>
                        <option value="DEBITO">Débito</option>
                      </select>
                    </label>
                  </div>
                )}
                {method === "CXC" && (
                  <Notice success>
                    La cuenta se cerrará como pendiente y aparecerá en Cuentas por cobrar y en el
                    corte como CXC.
                  </Notice>
                )}
                <p className="muted">
                  {method === "CXC"
                    ? "La cuenta ya no podrá modificarse desde caja."
                    : "Revisa el importe antes de confirmar. La cuenta cerrada ya no admite cambios."}
                </p>
              </>
            )}
          </>
        ) : (
          <>
            <p>
              {action === "remove"
                ? `${line?.quantity} × ${line?.name}. Se devolverán las piezas al inventario si es un insumo.`
                : "Los insumos de esta cuenta se devolverán al inventario del área. La cancelación quedará registrada."}
            </p>
            <label className="field">
              <span>Motivo</span>
              <textarea
                autoFocus
                required
                minLength={3}
                maxLength={250}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                rows={3}
              />
            </label>
          </>
        )}
        <div className="modal-actions">
          <button
            type="button"
            className="button button-secondary"
            disabled={busy}
            onClick={onClose}
          >
            Volver
          </button>
          <button
            className="button button-primary"
            disabled={busy || (action === "pay" && !method)}
          >
            {busy
              ? "Guardando…"
              : action === "pay"
                ? detail.category === "CORTESIA"
                  ? "Cerrar cuenta en $0.00"
                  : method === "CXC"
                    ? "Liberar cuenta y enviar a CXC"
                    : method === "TARJETA"
                      ? "Confirmar pago con tarjeta"
                      : "Confirmar cobro"
                : action === "remove"
                  ? "Retirar producto"
                  : "Confirmar cancelación"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

export function CutDialog({ id, onClose }: { id: string; onClose: () => void }) {
  const cut = useQuery({
    queryKey: ["cash-cut", id],
    queryFn: ({ signal }) => cashApi.cut(id, signal),
  });
  return (
    <Modal title="Recibo de corte de caja" onClose={onClose}>
      {cut.isPending ? (
        <p role="status">Preparando corte…</p>
      ) : cut.isError ? (
        <Notice>{cashError(cut.error)}</Notice>
      ) : (
        <div className="form-stack">
          <div className="cash-cut-heading">
            <ReceiptText size={30} />
            <div>
              <strong>
                Corte #{folio(cut.data.shift.number)} · {cut.data.shift.areaName}
              </strong>
              <span>{cut.data.shift.cashierName}</span>
              <small>
                {formatDate(cut.data.shift.openedAt)} — {formatDate(cut.data.shift.closedAt!)}
              </small>
            </div>
          </div>
          <div className="cash-cut-totals">
            <p>
              <span>Efectivo</span>
              <strong>{money(cut.data.shift.cashTotal)}</strong>
            </p>
            <p>
              <span>CXC</span>
              <strong>{money(cut.data.shift.cxcTotal)}</strong>
            </p>
            <p>
              <span>Tarjeta</span>
              <strong>{money(cut.data.shift.cardTotal)}</strong>
            </p>
            <p>
              <span>Efectivo recibido</span>
              <strong>{money(cut.data.shift.cashReceivedTotal)}</strong>
            </p>
            <p>
              <span>Cambio entregado</span>
              <strong>{money(cut.data.shift.changeGivenTotal)}</strong>
            </p>
            <p>
              <span>Total registrado (sin cortesías)</span>
              <strong>{money(cut.data.shift.total)}</strong>
            </p>
            <p>
              <span>Fondo de apertura</span>
              <strong>{money(cut.data.shift.openingAmount)}</strong>
            </p>
            <p>
              <span>Efectivo esperado</span>
              <strong>{money(cut.data.shift.expectedCash)}</strong>
            </p>
          </div>
          <p>
            {cut.data.shift.paidAccounts} cuentas cobradas o de cortesía ·{" "}
            {cut.data.shift.pendingAccounts} pendientes CXC · {cut.data.shift.cancelledAccounts}{" "}
            canceladas
          </p>
          <div className="cash-cut-accounts">
            {cut.data.accounts.map(
              ({
                account,
                amountReceived,
                changeGiven,
                cardLastFour,
                cardBank,
                cardType,
                cardAuthorization,
              }) => (
                <div key={account.id}>
                  <span>
                    #{folio(account.number)} · {account.clientName}
                    <small>
                      {account.status === "Pendiente"
                        ? "Pendiente CXC"
                        : account.paymentMethod === "CORTESIA"
                          ? "Cortesía · $0.00"
                          : account.status === "Pagada"
                            ? "Cerrada"
                            : account.status}{" "}
                      · {account.paymentMethod ?? "Sin cobro"}
                    </small>
                  </span>
                  <strong>{money(account.total)}</strong>
                  {amountReceived != null && (
                    <small>
                      Recibido {money(amountReceived)} · cambio {money(changeGiven ?? 0)}
                    </small>
                  )}
                  {cardLastFour && (
                    <small>
                      {cardType} · Banco {cardBank} · **** {cardLastFour} · Aut. {cardAuthorization}
                    </small>
                  )}
                </div>
              ),
            )}
          </div>
          <ReceiptButtons shiftId={id} />
        </div>
      )}
    </Modal>
  );
}
