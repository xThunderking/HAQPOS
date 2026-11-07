import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Barcode, Coffee, Package, Plus, ReceiptText, Search, Trash2, Wallet } from "lucide-react";
import { Notice } from "@/components/user-interface";
import { AccountActionDialog } from "@/components/cash-dialogs";
import {
  resolveCashCommands,
  useCashCommandId,
  useDebouncedSearch,
} from "@/hooks/use-cash-command";
import { formatDate } from "@/services/users";
import {
  cashApi,
  cashError,
  folio,
  money,
  type CashLine,
  type CashProduct,
  type CashShift,
} from "@/services/cash";

export function CashAccountPanel({
  id,
  shift,
  onChanged,
}: {
  id: string;
  shift: CashShift;
  onChanged: () => void;
}) {
  const client = useQueryClient();
  const detail = useQuery({
    queryKey: ["cash-account", id],
    queryFn: ({ signal }) => cashApi.account(id, signal),
  });
  const [search, setSearch] = useState("");
  useEffect(() => {
    if (detail.data) resolveCashCommands(detail.data.lines.map((line) => line.id));
  }, [detail.data]);
  const debounced = useDebouncedSearch(search);
  const [quantity, setQuantity] = useState("1");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [action, setAction] = useState<{
    type: "pay" | "remove" | "cancel";
    line?: CashLine;
  } | null>(null);
  const input = useRef<HTMLInputElement>(null);
  const command = useCashCommandId();
  const inFlight = useRef(false);
  const open = detail.data?.account.status === "Abierta";
  const products = useQuery({
    queryKey: ["cash-products", shift.id, debounced],
    queryFn: ({ signal }) => cashApi.products(shift.id, debounced, signal),
    enabled: Boolean(open && debounced),
    staleTime: 0,
  });
  function changed() {
    void client.invalidateQueries({ queryKey: ["cash-account", id] });
    void client.invalidateQueries({ queryKey: ["cash-products"] });
    onChanged();
  }
  async function add(product: CashProduct) {
    const count = Number(quantity);
    if (!Number.isInteger(count) || count < 1 || count > 100000)
      throw new Error("Ingresa una cantidad entera válida.");
    const result = await cashApi.add(
      id,
      command.for(JSON.stringify([id, product.source, product.id, count])),
      product,
      count,
    );
    command.clear();
    client.setQueryData(["cash-account", id], result);
    changed();
    setSuccess(
      `${count} × ${product.name} agregado${count === 1 ? "" : "s"} a la cuenta #${folio(result.account.number)}.`,
    );
    setSearch("");
    setQuantity("1");
  }
  async function perform(product?: CashProduct) {
    if (inFlight.current) return;
    inFlight.current = true;
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      if (product) await add(product);
      else {
        const found = await cashApi.products(shift.id, search.trim());
        const exact = found.filter((p) => p.code.toUpperCase() === search.trim().toUpperCase());
        client.setQueryData(["cash-products", shift.id, search.trim()], found);
        if (exact.length === 1) await add(exact[0]);
        else if (!found.length) setError("No hay productos disponibles con esos datos en tu área.");
      }
    } catch (failure) {
      setError(cashError(failure));
      changed();
    } finally {
      inFlight.current = false;
      setBusy(false);
      window.requestAnimationFrame(() => input.current?.focus());
    }
  }
  if (detail.isPending)
    return (
      <section className="panel cash-account-panel panel-state" role="status">
        Cargando cuenta…
      </section>
    );
  if (detail.isError)
    return (
      <section className="panel cash-account-panel">
        <Notice>{cashError(detail.error)}</Notice>
        <button className="button button-secondary" onClick={() => void detail.refetch()}>
          Reintentar
        </button>
      </section>
    );
  const value = detail.data;
  const lines = value.lines.filter((line) => !line.removedAt);
  return (
    <section className="panel cash-account-panel">
      <div className="cash-account-heading">
        <div>
          <span className="eyebrow">CUENTA #{folio(value.account.number)}</span>
          <h2>{value.account.clientName}</h2>
          <p>
            #{value.account.registrationNumber} ·{" "}
            {value.isExternal ? "Externo" : `Consultorio ${value.office}`} · {value.category}
          </p>
        </div>
        <span className={`status ${open ? "status-active" : "status-neutral"}`}>
          {value.account.status === "Pagada"
            ? value.account.paymentMethod === "CORTESIA"
              ? "Cortesía · cerrada"
              : "Cerrada"
            : value.account.status === "Pendiente"
              ? "Pendiente CXC"
              : value.account.status}
        </span>
      </div>
      {error && <Notice>{error}</Notice>}
      {success && <Notice success>{success}</Notice>}
      {open && (
        <div className="cash-product-search">
          <form
            className="cash-scan-form"
            onSubmit={(e) => {
              e.preventDefault();
              if (search.trim()) void perform();
            }}
          >
            <label className="field cash-scan-input">
              <span>Buscar o escanear producto de {shift.areaName}</span>
              <div className="search-input">
                <Barcode size={21} />
                <input
                  ref={input}
                  autoFocus
                  maxLength={100}
                  autoComplete="off"
                  spellCheck={false}
                  disabled={busy}
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setSuccess("");
                  }}
                  placeholder="Código de barras o nombre…"
                />
              </div>
            </label>
            <label className="field cash-quantity">
              <span>Cantidad</span>
              <input
                type="number"
                min="1"
                max="100000"
                step="1"
                required
                value={quantity}
                disabled={busy}
                onChange={(e) => setQuantity(e.target.value)}
              />
            </label>
            <button className="button button-secondary" disabled={busy || !search.trim()}>
              <Search size={18} />
              <span className="sr-only">Buscar producto</span>
            </button>
          </form>
          {debounced && (
            <div className="cash-product-results">
              {products.isPending ? (
                <p role="status">Buscando en tu área…</p>
              ) : products.isError ? (
                <Notice>{cashError(products.error)}</Notice>
              ) : products.data.length === 0 ? (
                <p>No encontramos productos en esta área.</p>
              ) : (
                products.data.map((product) => (
                  <button
                    key={`${product.source}-${product.id}`}
                    type="button"
                    disabled={busy || (product.stock !== null && product.stock < Number(quantity))}
                    className="cash-product-option"
                    onClick={() => void perform(product)}
                  >
                    <span className="cash-product-icon">
                      {product.source === "Menu" ? <Coffee size={20} /> : <Package size={20} />}
                    </span>
                    <span>
                      <strong>{product.name}</strong>
                      <small>
                        {product.code}
                        {product.presentation ? ` · ${product.presentation}` : ""} ·{" "}
                        {product.source === "Menu" ? "Menú" : `${product.stock} pzas disponibles`}
                      </small>
                    </span>
                    <strong>{money(product.price)}</strong>
                    <Plus size={18} />
                  </button>
                ))
              )}
            </div>
          )}
        </div>
      )}
      <div className="table-scroll">
        <table className="data-table cash-lines">
          <thead>
            <tr>
              <th>Producto</th>
              <th>Cant.</th>
              <th>Precio</th>
              <th>Importe</th>
              <th>
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line) => (
              <tr key={line.id}>
                <td>
                  <strong>{line.name}</strong>
                  <small>
                    {line.code}
                    {line.presentation ? ` · ${line.presentation}` : ""} ·{" "}
                    {line.source === "Menu" ? "Menú" : "Inventario"}
                  </small>
                </td>
                <td>{line.quantity}</td>
                <td>{money(line.unitPrice)}</td>
                <td>
                  <strong>{money(line.total)}</strong>
                </td>
                <td>
                  {open && (
                    <button
                      className="icon-button"
                      disabled={busy}
                      title="Retirar producto"
                      aria-label={`Retirar ${line.name}`}
                      onClick={() => setAction({ type: "remove", line })}
                    >
                      <Trash2 size={17} />
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {lines.length === 0 && (
        <div className="cash-empty-account">
          <ReceiptText size={31} />
          <h3>{open ? "La cuenta está lista" : "Sin productos activos"}</h3>
          <p>
            {open
              ? "Escanea un código o busca un producto para agregarlo."
              : "Los productos retirados se conservan en el historial."}
          </p>
        </div>
      )}
      {value.lines.some((line) => line.removedAt) && (
        <details className="cash-removed-lines">
          <summary>
            Productos retirados ({value.lines.filter((line) => line.removedAt).length})
          </summary>
          {value.lines
            .filter((line) => line.removedAt)
            .map((line) => (
              <p key={line.id}>
                {line.quantity} × {line.name} · {line.removalReason}
              </p>
            ))}
        </details>
      )}
      <div className="cash-account-footer">
        <div>
          <span>Total de la cuenta</span>
          <strong>{money(value.account.total)}</strong>
          {value.account.paymentMethod && <small>{value.account.paymentMethod}</small>}
          {value.amountReceived !== null && (
            <small>
              Recibido {money(value.amountReceived)} · cambio {money(value.changeGiven ?? 0)}
            </small>
          )}
          {value.cardLastFour && (
            <small>
              {value.cardType} · {value.cardBank} · **** {value.cardLastFour} · Aut.{" "}
              {value.cardAuthorization}
            </small>
          )}
        </div>
        {open ? (
          <div>
            <button
              className="button button-secondary"
              disabled={busy}
              onClick={() => setAction({ type: "cancel" })}
            >
              Cancelar cuenta
            </button>
            <button
              className="button button-primary"
              disabled={
                busy ||
                lines.length === 0 ||
                (value.category !== "CORTESIA" && value.account.total <= 0)
              }
              onClick={() => setAction({ type: "pay" })}
            >
              <Wallet size={18} />
              {value.category === "CORTESIA" ? "Cerrar en $0.00" : "Cobrar cuenta"}
            </button>
          </div>
        ) : (
          <p>{value.cancellationReason ?? `Cerrada el ${formatDate(value.account.closedAt!)}`}</p>
        )}
      </div>
      {action && (
        <AccountActionDialog
          detail={value}
          action={action.type}
          line={action.line}
          onClose={() => setAction(null)}
          onSaved={() => {
            setAction(null);
            changed();
            setSuccess("La cuenta se actualizó correctamente.");
          }}
        />
      )}
    </section>
  );
}
