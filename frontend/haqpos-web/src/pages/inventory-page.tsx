import { useEffect, useRef, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router";
import { useAuth } from "@/hooks/use-auth";
import {
  ArrowRight,
  Barcode,
  Boxes,
  History,
  MapPinned,
  PackagePlus,
  Pencil,
  Plus,
  Power,
  Search,
  SlidersHorizontal,
} from "lucide-react";
import { Modal, Notice, Pagination, SecurityNote } from "@/components/user-interface";
import { areasApi } from "@/services/areas";
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
  areaId,
  areaName,
  onClose,
  onSaved,
}: {
  item: InventoryItem | null;
  areaId: string;
  areaName: string;
  onClose: () => void;
  onSaved: (success: string) => void;
}) {
  const [code, setCode] = useState(item?.code ?? "");
  const [generateBarcode, setGenerateBarcode] = useState(false);
  const [name, setName] = useState(item?.name ?? "");
  const [presentation, setPresentation] = useState(item?.presentation ?? "");
  const [initialUnit, setInitialUnit] = useState("Pieza");
  const [unitPrice, setUnitPrice] = useState(item?.unitPrice == null ? "" : String(item.unitPrice));
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
        code: generateBarcode ? "" : code.trim(),
        name: name.trim(),
        presentation: presentation.trim() || null,
        minimumStock: Number(minimumStock),
        unitPrice: Number(unitPrice),
      };
      if (item) {
        await inventoryApi.update(areaId, item, common);
        onSaved("El insumo se actualizó correctamente.");
      } else {
        const created = await inventoryApi.create(areaId, {
          ...common,
          unit: initialUnit,
          generateBarcode,
          initialStock: Number(initialStock),
        });
        onSaved(
          generateBarcode
            ? `Insumo registrado. Código de barras: ${created.code}`
            : "El insumo se registró correctamente.",
        );
      }
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
        <div className="inventory-area-context">
          <MapPinned size={16} />
          <span>
            Inventario de <strong>{areaName}</strong>
          </span>
        </div>
        <div className="inventory-form-grid">
          <div className={`inventory-barcode-entry${item ? "" : " has-toggle"}`}>
            <label className="field">
              <span>Código de barras</span>
              <input
                required={!generateBarcode}
                disabled={generateBarcode}
                maxLength={30}
                value={generateBarcode ? "" : code}
                onChange={(event) => setCode(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") event.preventDefault();
                }}
                autoComplete="off"
                autoCapitalize="off"
                spellCheck={false}
                autoFocus={!item}
                placeholder={generateBarcode ? "Se generará al guardar" : "Escanea o escríbelo"}
              />
              {!generateBarcode && <small>Escanea o captura el código manualmente.</small>}
            </label>
            {!item && (
              <label className="inventory-barcode-toggle">
                <input
                  type="checkbox"
                  checked={generateBarcode}
                  disabled={busy}
                  onChange={(event) => setGenerateBarcode(event.target.checked)}
                />
                <span>Crear código de barras</span>
              </label>
            )}
          </div>
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
            <span>Presentación / contenido</span>
            <input
              maxLength={80}
              value={presentation}
              onChange={(event) => setPresentation(event.target.value)}
              placeholder="Ej. 600 ml, 1 litro, 250 g"
            />
            <small>Campo abierto; úsalo para indicar volumen, peso o contenido.</small>
          </label>
          {!item ? (
            <label className="field">
              <span>Presentación inicial</span>
              <select
                required
                value={initialUnit}
                onChange={(event) => setInitialUnit(event.target.value)}
              >
                <option value="Pieza">Pieza</option>
                <option value="Caja 12">Caja 12</option>
                <option value="Caja 24">Caja 24</option>
                <option value="Caja 48">Caja 48</option>
              </select>
            </label>
          ) : (
            <label className="field">
              <span>Unidad de inventario</span>
              <input value="Pieza" readOnly />
            </label>
          )}
          <label className="field">
            <span>Precio por pieza (MXN)</span>
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
            <span>Existencia mínima (piezas)</span>
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
              <span>Existencia inicial ({initialUnit})</span>
              <input
                type="number"
                inputMode="decimal"
                min="0"
                step="0.001"
                required
                value={initialStock}
                onChange={(event) => setInitialStock(event.target.value)}
              />
              <small>Se convertirá y guardará en piezas.</small>
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
  areaId,
  item,
  onClose,
  onSaved,
}: {
  areaId: string;
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
      await inventoryApi.setActive(areaId, item, isActive);
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
  areaId,
  item,
  onClose,
  onSaved,
}: {
  areaId: string;
  item: InventoryItem;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [type, setType] = useState<InventoryMovementType>("Entrada");
  const [quantity, setQuantity] = useState("");
  const [unit, setUnit] = useState("Pieza");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await inventoryApi.move(areaId, item, type, Number(quantity), reason.trim(), unit);
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
            {formatQuantity(item.currentStock)} <small>piezas</small>
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
              : "La cantidad se convierte a piezas antes de actualizar la existencia."}
          </small>
        </label>
        <label className="field">
          <span>{type === "Ajuste" ? "Existencia contada" : "Cantidad"}</span>
          <select value={unit} onChange={(event) => setUnit(event.target.value)}>
            <option value="Pieza">Pieza</option>
            <option value="Caja 12">Caja 12 (12 piezas)</option>
            <option value="Caja 24">Caja 24 (24 piezas)</option>
            <option value="Caja 48">Caja 48 (48 piezas)</option>
          </select>
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

function BarcodeEntryDialog({
  areaId,
  areaName,
  onClose,
  onSaved,
}: {
  areaId: string;
  areaName: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [barcode, setBarcode] = useState("");
  const [item, setItem] = useState<InventoryItem | null>(null);
  const [quantity, setQuantity] = useState("");
  const [unit, setUnit] = useState("Pieza");
  const [reason, setReason] = useState("Reposición de inventario");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const quantityInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (item?.isActive) quantityInput.current?.focus();
  }, [item]);

  async function findItem() {
    if (busy) return;
    const value = barcode.trim();
    if (!value) {
      setError("Escanea o captura un código de barras.");
      return;
    }
    setBusy(true);
    setError("");
    setItem(null);
    try {
      const found = await inventoryApi.findByBarcode(areaId, value);
      setItem(found);
    } catch (failure) {
      setError(inventoryErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!item) return;
    setBusy(true);
    setError("");
    try {
      await inventoryApi.move(areaId, item, "Entrada", Number(quantity), reason.trim(), unit);
      onSaved();
    } catch (failure) {
      setError(inventoryErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title="Agregar existencias"
      description={`Escanea o captura el código del insumo de ${areaName}.`}
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        <label className="field">
          <span>Código de barras</span>
          <div className="inventory-barcode-lookup">
            <input
              autoFocus
              required
              maxLength={30}
              autoComplete="off"
              autoCapitalize="off"
              spellCheck={false}
              value={barcode}
              onChange={(event) => {
                setBarcode(event.target.value);
                setItem(null);
                setQuantity("");
                setError("");
              }}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  void findItem();
                }
              }}
              placeholder="Escanea o captura el código"
            />
            <button
              type="button"
              className="button button-secondary"
              disabled={busy || !barcode.trim()}
              onClick={() => void findItem()}
            >
              <Barcode size={17} /> Buscar
            </button>
          </div>
          <small>Al terminar el escaneo, la pistola puede enviar Enter para buscar.</small>
        </label>
        {item && (
          <>
            <div className="inventory-barcode-result">
              <div>
                <strong>{item.name}</strong>
                <span>{item.code} · Pieza</span>
              </div>
              <div className="inventory-current-stock">
                <span>Existencia actual</span>
                <strong>
                  {formatQuantity(item.currentStock)} <small>piezas</small>
                </strong>
              </div>
            </div>
            {!item.isActive ? (
              <Notice>Este insumo está inactivo. Actívalo antes de agregar existencias.</Notice>
            ) : (
              <>
                <label className="field">
                  <span>Presentación recibida</span>
                  <select value={unit} onChange={(event) => setUnit(event.target.value)}>
                    <option value="Pieza">Pieza</option>
                    <option value="Caja 12">Caja 12 (12 piezas)</option>
                    <option value="Caja 24">Caja 24 (24 piezas)</option>
                    <option value="Caja 48">Caja 48 (48 piezas)</option>
                  </select>
                  <span>Cantidad a agregar ({unit})</span>
                  <input
                    ref={quantityInput}
                    type="number"
                    inputMode="decimal"
                    min="0.001"
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
                  />
                </label>
              </>
            )}
          </>
        )}
        <SecurityNote>
          La entrada quedará registrada en el historial de {areaName} con tu usuario y la hora.
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
          <button className="button button-primary" disabled={busy || !item?.isActive}>
            {busy ? "Guardando…" : "Registrar entrada"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function MovementHistory({
  areaId,
  item,
  onClose,
}: {
  areaId: string;
  item: InventoryItem;
  onClose: () => void;
}) {
  const [page, setPage] = useState(1);
  const movements = useQuery({
    queryKey: ["inventory-movements", areaId, item.id, page],
    queryFn: ({ signal }) => inventoryApi.movements(areaId, item, page, signal),
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
                      {movement.inputQuantity !== null && movement.inputUnit ? (
                        <>
                          {movement.quantity > 0 ? "+" : ""}
                          {formatQuantity(movement.quantity)} piezas ·{" "}
                          {formatQuantity(movement.inputQuantity)} {movement.inputUnit}
                        </>
                      ) : (
                        <>{formatQuantity(movement.quantity)} · unidad histórica no registrada</>
                      )}
                    </td>
                    <td>
                      {formatQuantity(movement.stockBefore)} → {formatQuantity(movement.stockAfter)}
                      {movement.inputQuantity === null && (
                        <small> · unidad histórica no registrada</small>
                      )}
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
  const { areaId } = useParams();
  return <InventoryAreaPage key={areaId ?? "areas"} selectedAreaId={areaId ?? null} />;
}

function InventoryAreaPage({ selectedAreaId }: { selectedAreaId: string | null }) {
  const { user } = useAuth();
  const canManageInventory = user?.role === "Administrador" || user?.role === "Supervisor";
  const client = useQueryClient();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<{ item: InventoryItem | null } | null>(null);
  const [movementItem, setMovementItem] = useState<InventoryItem | null>(null);
  const [historyItem, setHistoryItem] = useState<InventoryItem | null>(null);
  const [statusItem, setStatusItem] = useState<InventoryItem | null>(null);
  const [barcodeEntryOpen, setBarcodeEntryOpen] = useState(false);
  const [message, setMessage] = useState("");
  const areas = useQuery({
    queryKey: ["area-options"],
    queryFn: ({ signal }) => areasApi.options(signal),
  });
  const selectedArea = areas.data?.find((area) => area.id === selectedAreaId);
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);
  const inventory = useQuery({
    queryKey: ["inventory", selectedAreaId, debouncedSearch, page],
    queryFn: ({ signal }) => inventoryApi.list(selectedAreaId!, debouncedSearch, page, signal),
    enabled: selectedAreaId !== null,
  });

  function chooseArea(areaId: string) {
    void navigate(`/inventario/areas/${areaId}`);
    setSearch("");
    setDebouncedSearch("");
    setPage(1);
    setMessage("");
  }

  function saved(success: string) {
    setEditor(null);
    setMovementItem(null);
    setStatusItem(null);
    setBarcodeEntryOpen(false);
    setMessage(success);
    void client.invalidateQueries({ queryKey: ["inventory"] });
    void client.invalidateQueries({ queryKey: ["inventory-movements"] });
    void client.invalidateQueries({ queryKey: ["audit"] });
  }

  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ABASTECIMIENTO</span>
          <h1>Inventario</h1>
          <p>
            {selectedArea
              ? `Inventario del área ${selectedArea.name}. Consulta existencias y movimientos.`
              : "Selecciona un área para consultar su inventario."}
          </p>
        </div>
        <div className="page-heading-actions">
          {selectedAreaId && (
            <button className="button button-secondary" onClick={() => navigate("/inventario")}>
              <MapPinned size={17} /> Cambiar área
            </button>
          )}
          {canManageInventory && selectedArea && (
            <button
              className="button button-secondary"
              onClick={() => {
                setBarcodeEntryOpen(true);
                setMessage("");
              }}
            >
              <Barcode size={17} /> Agregar por código
            </button>
          )}
          {canManageInventory && selectedArea && (
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
      </div>
      {message && <Notice success>{message}</Notice>}
      {!selectedAreaId && (
        <section className="panel inventory-area-panel">
          <div className="panel-toolbar">
            <div>
              <h2>
                <MapPinned size={18} /> Selecciona un área
              </h2>
              <p>Cada área tiene sus propios insumos, existencias e historial.</p>
            </div>
          </div>
          {areas.isPending ? (
            <div className="panel-state" role="status">
              Cargando áreas…
            </div>
          ) : areas.isError ? (
            <div className="panel-state">
              <Notice>{inventoryErrorMessage(areas.error)}</Notice>
              <button className="button button-secondary" onClick={() => void areas.refetch()}>
                Reintentar
              </button>
            </div>
          ) : areas.data.length === 0 ? (
            <div className="panel-state">
              <span className="empty-icon">
                <MapPinned size={28} />
              </span>
              <h3>Aún no hay áreas registradas</h3>
              <p>Registra un área en el módulo Áreas para comenzar a usar el inventario.</p>
            </div>
          ) : (
            <div className="inventory-area-grid">
              {areas.data.map((area) => (
                <button
                  key={area.id}
                  type="button"
                  className="inventory-area-card"
                  onClick={() => chooseArea(area.id)}
                >
                  <span className="inventory-area-icon">
                    <Boxes size={22} />
                  </span>
                  <strong>{area.name}</strong>
                  <span>Ver inventario</span>
                  <ArrowRight size={18} aria-hidden="true" />
                </button>
              ))}
            </div>
          )}
        </section>
      )}
      {selectedAreaId && !selectedArea && areas.isPending && (
        <section className="panel-state" role="status">
          Cargando el área…
        </section>
      )}
      {selectedAreaId && !selectedArea && areas.isError && (
        <section className="panel-state">
          <Notice>{inventoryErrorMessage(areas.error)}</Notice>
          <button className="button button-secondary" onClick={() => void areas.refetch()}>
            Reintentar
          </button>
        </section>
      )}
      {selectedAreaId && areas.data && !selectedArea && (
        <section className="panel-state">
          <Notice>El área solicitada no está registrada.</Notice>
          <button className="button button-secondary" onClick={() => navigate("/inventario")}>
            Volver a las áreas
          </button>
        </section>
      )}
      {selectedAreaId && selectedArea && (
        <>
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
                  placeholder="Buscar código de barras o nombre…"
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
                <button
                  className="button button-secondary"
                  onClick={() => void inventory.refetch()}
                >
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
                    ? "Prueba con otro código de barras o nombre."
                    : "Registra un insumo para comenzar a controlar sus existencias."}
                </p>
                {!debouncedSearch && canManageInventory && (
                  <button
                    className="button button-primary"
                    onClick={() => setEditor({ item: null })}
                  >
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
                        <th>Precio por pieza</th>
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
                                <span>{item.code} · Pieza</span>
                                {item.presentation && <span>{item.presentation}</span>}
                              </div>
                            </div>
                          </td>
                          <td>{formatUnitPrice(item.unitPrice)}</td>
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
                            <span className="muted">piezas</span>
                          </td>
                          <td>
                            {formatQuantity(item.minimumStock)}{" "}
                            <span className="muted">piezas</span>
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
            Los movimientos no se eliminan ni modifican. Para corregir diferencias, registra un
            ajuste con su motivo.
          </SecurityNote>
        </>
      )}
      {editor && (
        <ItemEditor
          item={editor.item}
          areaId={selectedAreaId!}
          areaName={selectedArea?.name ?? "el área seleccionada"}
          onClose={() => setEditor(null)}
          onSaved={saved}
        />
      )}
      {movementItem && (
        <MovementDialog
          areaId={selectedAreaId!}
          item={movementItem}
          onClose={() => setMovementItem(null)}
          onSaved={() => saved("El movimiento se guardó correctamente.")}
        />
      )}
      {barcodeEntryOpen && selectedAreaId && selectedArea && (
        <BarcodeEntryDialog
          areaId={selectedAreaId}
          areaName={selectedArea.name}
          onClose={() => setBarcodeEntryOpen(false)}
          onSaved={() => saved("Las existencias se agregaron correctamente.")}
        />
      )}
      {statusItem && (
        <StatusDialog
          areaId={selectedAreaId!}
          item={statusItem}
          onClose={() => setStatusItem(null)}
          onSaved={(isActive) =>
            saved(isActive ? "El insumo fue activado." : "El insumo fue desactivado.")
          }
        />
      )}
      {historyItem && (
        <MovementHistory
          areaId={selectedAreaId!}
          item={historyItem}
          onClose={() => setHistoryItem(null)}
        />
      )}
    </div>
  );
}
