import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { History, PackagePlus, Pencil, Plus, Power, Search, SlidersHorizontal } from "lucide-react";
import { Modal, Notice, Pagination, SecurityNote } from "@/components/user-interface";
import {
  formatQuantity,
  formatUnitPrice,
  inventoryApi,
  inventoryErrorMessage,
  type InventoryItem,
  type InventoryMovementType,
} from "@/services/inventory";
import { formatDate } from "@/services/users";

function StockStatus({ item }: { item: InventoryItem }) {
  if (!item.isActive) return <span className="status status-neutral">Inactivo</span>;
  if (item.currentStock <= item.minimumStock)
    return <span className="status status-warning">Existencia baja</span>;
  return <span className="status status-active">Disponible</span>;
}

function ItemEditor({
  item,
  onClose,
  onSaved,
}: {
  item: InventoryItem | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [code, setCode] = useState(item?.code ?? "");
  const [name, setName] = useState(item?.name ?? "");
  const [category, setCategory] = useState(item?.category ?? "");
  const [unit, setUnit] = useState(item?.unit ?? "Pieza");
  const [unitPrice, setUnitPrice] = useState(item?.unitPrice == null ? "" : String(item.unitPrice));
  const [location, setLocation] = useState(item?.location ?? "");
  const [minimumStock, setMinimumStock] = useState(String(item?.minimumStock ?? 0));
  const [initialStock, setInitialStock] = useState("0");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const common = {
        code: code.trim(),
        name: name.trim(),
        category: category.trim(),
        unit: unit.trim(),
        location: location.trim() || null,
        minimumStock: Number(minimumStock),
        unitPrice: Number(unitPrice),
      };
      if (item) await inventoryApi.update(item, common);
      else await inventoryApi.create({ ...common, initialStock: Number(initialStock) });
      onSaved();
    } catch (failure) {
      setError(inventoryErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title={item ? "Editar insumo" : "Registrar insumo"}
      description={
        item
          ? "Actualiza los datos del artículo."
          : "Define el artículo y su existencia de arranque."
      }
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        <div className="inventory-form-grid">
          <label className="field">
            <span>Clave</span>
            <input
              required
              maxLength={30}
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder="Ej. GUANTE-NITRILO-M"
            />
          </label>
          <label className="field">
            <span>Nombre del insumo</span>
            <input
              required
              minLength={2}
              maxLength={120}
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Ej. Guantes de nitrilo medianos"
            />
          </label>
          <label className="field">
            <span>Categoría</span>
            <input
              required
              minLength={2}
              maxLength={80}
              value={category}
              onChange={(event) => setCategory(event.target.value)}
              placeholder="Ej. Protección personal"
            />
          </label>
          <label className="field">
            <span>Unidad de medida</span>
            <input
              required
              minLength={2}
              maxLength={24}
              value={unit}
              onChange={(event) => setUnit(event.target.value)}
              placeholder="Pieza, caja, paquete…"
            />
          </label>
          <label className="field">
            <span>Precio unitario (MXN)</span>
            <input
              type="number"
              inputMode="decimal"
              min="0.01"
              max="999999.99"
              step="0.01"
              required
              value={unitPrice}
              onChange={(event) => setUnitPrice(event.target.value)}
              placeholder="0.00"
            />
          </label>
          <label className="field">
            <span>Ubicación</span>
            <input
              maxLength={80}
              value={location}
              onChange={(event) => setLocation(event.target.value)}
              placeholder="Almacén o área"
            />
          </label>
          <label className="field">
            <span>Existencia mínima</span>
            <input
              type="number"
              inputMode="decimal"
              min="0"
              step="0.001"
              required
              value={minimumStock}
              onChange={(event) => setMinimumStock(event.target.value)}
            />
          </label>
          {!item && (
            <label className="field">
              <span>Existencia inicial</span>
              <input
                type="number"
                inputMode="decimal"
                min="0"
                step="0.001"
                required
                value={initialStock}
                onChange={(event) => setInitialStock(event.target.value)}
              />
              <small>
                Si registras unidades, se guardará como entrada inicial en el historial.
              </small>
            </label>
          )}
        </div>
        <div className="modal-actions">
          <button
            type="button"
            className="button button-secondary"
            disabled={busy}
            onClick={onClose}
          >
            Cancelar
          </button>
          <button className="button button-primary" disabled={busy}>
            {busy ? "Guardando…" : item ? "Guardar cambios" : "Registrar insumo"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function StatusDialog({
  item,
  onClose,
  onSaved,
}: {
  item: InventoryItem;
  onClose: () => void;
  onSaved: (isActive: boolean) => void;
}) {
  const isActive = !item.isActive;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function changeStatus() {
    setBusy(true);
    setError("");
    try {
      await inventoryApi.setActive(item, isActive);
      onSaved(isActive);
    } catch (failure) {
      setError(inventoryErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title={isActive ? "Activar insumo" : "Desactivar insumo"}
      description={`${item.code} · ${item.name}`}
      busy={busy}
      onClose={onClose}
    >
      <p>
        {isActive
          ? "El insumo volverá a permitir el registro de entradas, salidas y ajustes."
          : "El insumo dejará de permitir nuevos movimientos. Sus datos y su historial se conservarán."}
      </p>
      {error && <Notice>{error}</Notice>}
      <SecurityNote>
        El cambio quedará registrado con tu usuario en la actividad de seguridad.
      </SecurityNote>
      <div className="modal-actions">
        <button className="button button-secondary" disabled={busy} onClick={onClose}>
          Cancelar
        </button>
        <button
          className="button button-primary"
          disabled={busy}
          onClick={() => void changeStatus()}
        >
          {busy ? "Guardando…" : isActive ? "Confirmar activación" : "Confirmar desactivación"}
        </button>
      </div>
    </Modal>
  );
}

function MovementDialog({
  item,
  onClose,
  onSaved,
}: {
  item: InventoryItem;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [type, setType] = useState<InventoryMovementType>("Entrada");
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await inventoryApi.move(item, type, Number(quantity), reason.trim());
      onSaved();
    } catch (failure) {
      setError(inventoryErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title="Registrar movimiento"
      description={`${item.code} · ${item.name}`}
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        <div className="inventory-current-stock">
          <span>Existencia actual</span>
          <strong>
            {formatQuantity(item.currentStock)} <small>{item.unit}</small>
          </strong>
        </div>
        <label className="field">
          <span>Tipo de movimiento</span>
          <select
            value={type}
            onChange={(event) => setType(event.target.value as InventoryMovementType)}
          >
            <option value="Entrada">Entrada · recibir existencias</option>
            <option value="Salida">Salida · entregar o consumir</option>
            <option value="Ajuste">Ajuste · registrar conteo físico</option>
          </select>
          <small>
            {type === "Ajuste"
              ? "La cantidad será la existencia total contada."
              : `La cantidad se suma o resta en ${item.unit}.`}
          </small>
        </label>
        <label className="field">
          <span>{type === "Ajuste" ? "Existencia contada" : "Cantidad"}</span>
          <input
            type="number"
            inputMode="decimal"
            min={type === "Ajuste" ? "0" : "0.001"}
            step="0.001"
            required
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
          />
        </label>
        <label className="field">
          <span>Motivo</span>
          <input
            required
            minLength={3}
            maxLength={250}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Ej. Reposición de almacén"
          />
        </label>
        <SecurityNote>
          El movimiento queda registrado con tu usuario, la hora y el saldo anterior y nuevo.
        </SecurityNote>
        <div className="modal-actions">
          <button
            type="button"
            className="button button-secondary"
            disabled={busy}
            onClick={onClose}
          >
            Cancelar
          </button>
          <button className="button button-primary" disabled={busy}>
            {busy ? "Guardando…" : "Guardar movimiento"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function MovementHistory({ item, onClose }: { item: InventoryItem; onClose: () => void }) {
  const [page, setPage] = useState(1);
  const movements = useQuery({
    queryKey: ["inventory-movements", item.id, page],
    queryFn: ({ signal }) => inventoryApi.movements(item, page, signal),
  });
  return (
    <Modal
      title="Historial de movimientos"
      description={`${item.code} · ${item.name}`}
      onClose={onClose}
    >
      {movements.isPending ? (
        <div className="panel-state">Cargando movimientos…</div>
      ) : movements.isError ? (
        <div className="panel-state">
          <Notice>{inventoryErrorMessage(movements.error)}</Notice>
          <button className="button button-secondary" onClick={() => void movements.refetch()}>
            Reintentar
          </button>
        </div>
      ) : movements.data.items.length === 0 ? (
        <div className="panel-state">Este insumo todavía no tiene movimientos.</div>
      ) : (
        <>
          <div className="table-scroll inventory-history-scroll">
            <table className="data-table inventory-history-table">
              <thead>
                <tr>
                  <th>Movimiento</th>
                  <th>Cambio</th>
                  <th>Existencia</th>
                  <th>Motivo y responsable</th>
                  <th>Fecha</th>
                </tr>
              </thead>
              <tbody>
                {movements.data.items.map((movement) => (
                  <tr key={movement.id}>
                    <td>
                      <span className={`movement-type movement-${movement.type.toLowerCase()}`}>
                        {movement.type}
                      </span>
                    </td>
                    <td>
                      {movement.quantity > 0 ? "+" : ""}
                      {formatQuantity(movement.quantity)} {item.unit}
                    </td>
                    <td>
                      {formatQuantity(movement.stockBefore)} → {formatQuantity(movement.stockAfter)}
                    </td>
                    <td>
                      <strong>{movement.reason}</strong>
                      <small className="inventory-actor">
                        {movement.actorFullName} · #{movement.actorEmployeeNumber}
                      </small>
                    </td>
                    <td className="date-cell">{formatDate(movement.occurredAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={movements.data.page}
            total={movements.data.total}
            pageSize={movements.data.pageSize}
            onChange={setPage}
          />
        </>
      )}
    </Modal>
  );
}

export function InventoryPage() {
  const { user } = useAuth();
  const canManageInventory = user?.role === "Administrador" || user?.role === "Supervisor";
  const client = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<{ item: InventoryItem | null } | null>(null);
  const [movementItem, setMovementItem] = useState<InventoryItem | null>(null);
  const [historyItem, setHistoryItem] = useState<InventoryItem | null>(null);
  const [statusItem, setStatusItem] = useState<InventoryItem | null>(null);
  const [message, setMessage] = useState("");
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);
  const inventory = useQuery({
    queryKey: ["inventory", debouncedSearch, page],
    queryFn: ({ signal }) => inventoryApi.list(debouncedSearch, page, signal),
  });

  function saved(success: string) {
    setEditor(null);
    setMovementItem(null);
    setStatusItem(null);
    setMessage(success);
    void client.invalidateQueries({ queryKey: ["inventory"] });
    void client.invalidateQueries({ queryKey: ["inventory-movements"] });
  }

  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ABASTECIMIENTO</span>
          <h1>Inventario</h1>
          <p>Consulta existencias y registra cada entrada, salida o ajuste.</p>
        </div>
        {canManageInventory && (
          <button
            className="button button-primary"
            onClick={() => {
              setEditor({ item: null });
              setMessage("");
            }}
          >
            <Plus size={18} /> Nuevo insumo
          </button>
        )}
      </div>
      {message && <Notice success>{message}</Notice>}
      <section className="panel inventory-panel">
        <div className="panel-toolbar">
          <div>
            <h2>
              <SlidersHorizontal size={17} /> Catálogo de insumos
            </h2>
            <p>El historial conserva la persona responsable y cada cambio de existencia.</p>
          </div>
          <label className="search-input">
            <Search size={18} aria-hidden="true" />
            <input
              type="search"
              aria-label="Buscar insumo"
              placeholder="Buscar clave, nombre o categoría…"
              maxLength={100}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
        </div>
        {inventory.isPending ? (
          <div className="panel-state" role="status">
            Cargando inventario…
          </div>
        ) : inventory.isError ? (
          <div className="panel-state">
            <Notice>{inventoryErrorMessage(inventory.error)}</Notice>
            <button className="button button-secondary" onClick={() => void inventory.refetch()}>
              Reintentar
            </button>
          </div>
        ) : inventory.data.items.length === 0 ? (
          <div className="panel-state">
            <span className="empty-icon">
              <PackagePlus size={28} />
            </span>
            <h3>{debouncedSearch ? "No encontramos insumos" : "El inventario está vacío"}</h3>
            <p>
              {debouncedSearch
                ? "Prueba con otra clave, nombre o categoría."
                : "Registra un insumo para comenzar a controlar sus existencias."}
            </p>
            {!debouncedSearch && canManageInventory && (
              <button className="button button-primary" onClick={() => setEditor({ item: null })}>
                <Plus size={17} /> Registrar primer insumo
              </button>
            )}
          </div>
        ) : (
          <>
            <div className="table-scroll">
              <table className="data-table inventory-table">
                <thead>
                  <tr>
                    <th>Insumo</th>
                    <th>Precio unitario</th>
                    <th>Categoría</th>
                    <th>Ubicación</th>
                    <th>Existencia</th>
                    <th>Mínimo</th>
                    <th>Estado</th>
                    <th>
                      <span className="sr-only">Acciones</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {inventory.data.items.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <div className="inventory-item-cell">
                          <span className="avatar">
                            <PackagePlus size={18} />
                          </span>
                          <div>
                            <strong>{item.name}</strong>
                            <span>
                              {item.code} · {item.unit}
                            </span>
                          </div>
                        </div>
                      </td>
                      <td>{formatUnitPrice(item.unitPrice)}</td>
                      <td>{item.category}</td>
                      <td>{item.location || <span className="muted">Sin ubicación</span>}</td>
                      <td>
                        <strong
                          className={
                            item.currentStock <= item.minimumStock && item.isActive
                              ? "inventory-low-stock"
                              : "inventory-stock"
                          }
                        >
                          {formatQuantity(item.currentStock)}
                        </strong>{" "}
                        <span className="muted">{item.unit}</span>
                      </td>
                      <td>
                        {formatQuantity(item.minimumStock)}{" "}
                        <span className="muted">{item.unit}</span>
                      </td>
                      <td>
                        <StockStatus item={item} />
                      </td>
                      <td>
                        <div className="row-actions">
                          {canManageInventory && (
                            <>
                              <button
                                className="icon-button inventory-toggle-button"
                                data-active={item.isActive}
                                title={item.isActive ? "Desactivar insumo" : "Activar insumo"}
                                aria-label={`${item.isActive ? "Desactivar" : "Activar"} ${item.name}`}
                                onClick={() => setStatusItem(item)}
                              >
                                <Power size={17} />
                              </button>
                              <button
                                className="icon-button"
                                title="Registrar movimiento"
                                aria-label={`Registrar movimiento para ${item.name}`}
                                disabled={!item.isActive}
                                onClick={() => setMovementItem(item)}
                              >
                                <PackagePlus size={17} />
                              </button>
                            </>
                          )}
                          <button
                            className="icon-button"
                            title="Ver historial"
                            aria-label={`Ver historial de ${item.name}`}
                            onClick={() => setHistoryItem(item)}
                          >
                            <History size={17} />
                          </button>
                          {canManageInventory && (
                            <button
                              className="icon-button"
                              title="Editar insumo"
                              aria-label={`Editar ${item.name}`}
                              onClick={() => setEditor({ item })}
                            >
                              <Pencil size={17} />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={inventory.data.page}
              total={inventory.data.total}
              pageSize={inventory.data.pageSize}
              onChange={setPage}
            />
          </>
        )}
      </section>
      <SecurityNote>
        Los movimientos no se eliminan ni modifican. Para corregir diferencias, registra un ajuste
        con su motivo.
      </SecurityNote>
      {editor && (
        <ItemEditor
          item={editor.item}
          onClose={() => setEditor(null)}
          onSaved={() =>
            saved(
              editor.item
                ? "El insumo se actualizó correctamente."
                : "El insumo se registró correctamente.",
            )
          }
        />
      )}
      {movementItem && (
        <MovementDialog
          item={movementItem}
          onClose={() => setMovementItem(null)}
          onSaved={() => saved("El movimiento se guardó correctamente.")}
        />
      )}
      {statusItem && (
        <StatusDialog
          item={statusItem}
          onClose={() => setStatusItem(null)}
          onSaved={(isActive) =>
            saved(isActive ? "El insumo fue activado." : "El insumo fue desactivado.")
          }
        />
      )}
      {historyItem && <MovementHistory item={historyItem} onClose={() => setHistoryItem(null)} />}
    </div>
  );
}
