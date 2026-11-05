import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router";
import { useAuth } from "@/hooks/use-auth";
import {
  ArrowRight,
  Barcode,
  Coffee,
  MapPinned,
  Pencil,
  Plus,
  Power,
  Search,
  Utensils,
} from "lucide-react";
import { Modal, Notice, Pagination, SecurityNote } from "@/components/user-interface";
import { areasApi, type AreaOption } from "@/services/areas";
import { formatPrice, menuApi, menuErrorMessage, type MenuProduct } from "@/services/menu";

function ProductEditor({
  product,
  areaId,
  areaName,
  onClose,
  onSaved,
}: {
  product: MenuProduct | null;
  areaId: string;
  areaName: string;
  onClose: () => void;
  onSaved: (message?: string) => void;
}) {
  const [name, setName] = useState(product?.name ?? "");
  const [category, setCategory] = useState(product?.category ?? "Comida");
  const [description, setDescription] = useState(product?.description ?? "");
  const [price, setPrice] = useState(product ? String(product.price) : "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const common = {
        name: name.trim(),
        category: category.trim(),
        description: description.trim() || null,
        price: Number(price),
      };
      if (product) {
        await menuApi.update(areaId, product, common);
        onSaved("El producto se actualizó correctamente.");
      } else {
        const created = await menuApi.create(areaId, common);
        onSaved(`Producto registrado. Código de barras: ${created.code}`);
      }
    } catch (failure) {
      setError(menuErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title={product ? "Editar producto" : "Registrar producto"}
      description={
        product
          ? "Actualiza el producto. Su código de barras del sistema se conserva."
          : "El sistema generará un código de barras numérico único de 9 dígitos al registrar este producto."
      }
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        <div className="inventory-area-context">
          <MapPinned size={16} /> Menú de <strong>{areaName}</strong>
        </div>
        {product ? (
          <label className="field">
            <span>Código de barras del sistema</span>
            <input readOnly value={product.code} />
            <small>Este código se mantiene asociado al producto.</small>
          </label>
        ) : (
          <div className="menu-barcode-generated">
            <Barcode size={19} aria-hidden="true" />
            <div>
              <strong>Código de barras automático</strong>
              <span>HAQPOS asignará un código numérico único de 9 dígitos al guardar.</span>
            </div>
          </div>
        )}
        <div className="inventory-form-grid">
          <label className="field">
            <span>Nombre</span>
            <input
              required
              minLength={2}
              maxLength={120}
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Ej. Agua de jamaica"
            />
          </label>
          <label className="field">
            <span>Categoría</span>
            <input
              required
              minLength={2}
              maxLength={60}
              value={category}
              onChange={(event) => setCategory(event.target.value)}
              placeholder="Comida o bebida"
            />
          </label>
          <label className="field">
            <span>Precio de venta (MXN)</span>
            <input
              type="number"
              inputMode="decimal"
              min="0.01"
              max="999999.99"
              step="0.01"
              required
              value={price}
              onChange={(event) => setPrice(event.target.value)}
              placeholder="0.00"
            />
          </label>
          <label className="field inventory-description-field">
            <span>Descripción (opcional)</span>
            <textarea
              maxLength={250}
              rows={3}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Presentación o detalle para identificarlo"
            />
          </label>
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
            {busy ? "Guardando…" : product ? "Guardar cambios" : "Registrar producto"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function ProductStatusDialog({
  product,
  areaId,
  onClose,
  onSaved,
}: {
  product: MenuProduct;
  areaId: string;
  onClose: () => void;
  onSaved: (isActive: boolean) => void;
}) {
  const isActive = !product.isActive;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function changeStatus() {
    setBusy(true);
    setError("");
    try {
      await menuApi.setActive(areaId, product, isActive);
      onSaved(isActive);
    } catch (failure) {
      setError(menuErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title={isActive ? "Activar producto" : "Desactivar producto"}
      description={`${product.code} · ${product.name}`}
      busy={busy}
      onClose={onClose}
    >
      <p>
        {isActive
          ? "El producto volverá a estar disponible en el menú."
          : "El producto dejará de estar disponible. Sus datos y cambios de precio se conservarán."}
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

function UnassignedProductRow({
  product,
  areas,
  onAssigned,
}: {
  product: MenuProduct;
  areas: AreaOption[];
  onAssigned: () => void;
}) {
  const [areaId, setAreaId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function assign() {
    if (!areaId) return;
    setBusy(true);
    setError("");
    try {
      await menuApi.assignArea(product, areaId);
      onAssigned();
    } catch (failure) {
      setError(menuErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="menu-unassigned-row">
      <div>
        <strong>{product.name}</strong>
        <small>{product.code}</small>
      </div>
      <label className="field">
        <span className="sr-only">Área para {product.name}</span>
        <select value={areaId} onChange={(event) => setAreaId(event.target.value)} disabled={busy}>
          <option value="">Selecciona un área</option>
          {areas.map((area) => (
            <option key={area.id} value={area.id}>
              {area.name}
            </option>
          ))}
        </select>
      </label>
      <button
        className="button button-secondary"
        disabled={!areaId || busy}
        onClick={() => void assign()}
      >
        {busy ? "Asignando…" : "Asignar"}
      </button>
      {error && <Notice>{error}</Notice>}
    </div>
  );
}

export function MenuPage() {
  const { areaId } = useParams();
  return <MenuAreaPage key={areaId ?? "areas"} selectedAreaId={areaId} />;
}

function MenuAreaPage({ selectedAreaId }: { selectedAreaId: string | undefined }) {
  const { user } = useAuth();
  const canManageMenu = user?.role === "Administrador" || user?.role === "Supervisor";
  const client = useQueryClient();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<{ product: MenuProduct | null } | null>(null);
  const [statusProduct, setStatusProduct] = useState<MenuProduct | null>(null);
  const [message, setMessage] = useState("");
  const [unassignedPage, setUnassignedPage] = useState(1);
  const areas = useQuery({
    queryKey: ["area-options"],
    queryFn: ({ signal }) => areasApi.options(signal),
  });
  const selectedArea = areas.data?.find((area) => area.id === selectedAreaId);
  const unassigned = useQuery({
    queryKey: ["menu-unassigned", unassignedPage],
    queryFn: ({ signal }) => menuApi.unassigned(unassignedPage, signal),
    enabled: !selectedAreaId,
  });
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);
  const products = useQuery({
    queryKey: ["menu-products", selectedAreaId, debouncedSearch, page],
    queryFn: ({ signal }) => menuApi.list(selectedAreaId!, debouncedSearch, page, signal),
    enabled: Boolean(selectedAreaId),
  });

  function saved(success = "El producto se guardó correctamente.") {
    setEditor(null);
    setStatusProduct(null);
    setMessage(success);
    void client.invalidateQueries({ queryKey: ["menu-products"] });
    void client.invalidateQueries({ queryKey: ["menu-unassigned"] });
    void client.invalidateQueries({ queryKey: ["audit"] });
  }

  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">PRODUCTOS DE VENTA</span>
          <h1>Menú</h1>
          <p>
            {selectedArea
              ? `Menú del área ${selectedArea.name}.`
              : "Selecciona un área para consultar su menú."}
          </p>
        </div>
        <div className="page-heading-actions">
          {selectedArea && (
            <button className="button button-secondary" onClick={() => void navigate("/menu")}>
              <MapPinned size={17} /> Cambiar área
            </button>
          )}
          {canManageMenu && selectedArea && (
            <button
              className="button button-primary"
              onClick={() => {
                setEditor({ product: null });
                setMessage("");
              }}
            >
              <Plus size={18} /> Nuevo producto
            </button>
          )}
        </div>
      </div>
      {message && <Notice success>{message}</Notice>}
      {!selectedAreaId && (
        <>
          <section className="panel inventory-area-panel">
            <div className="panel-toolbar">
              <div>
                <h2>
                  <MapPinned size={18} /> Selecciona un área
                </h2>
                <p>Cada área tiene su propio menú.</p>
              </div>
            </div>
            {areas.isPending ? (
              <div className="panel-state" role="status">
                Cargando áreas…
              </div>
            ) : areas.isError ? (
              <div className="panel-state">
                <Notice>{menuErrorMessage(areas.error)}</Notice>
                <button className="button button-secondary" onClick={() => void areas.refetch()}>
                  Reintentar
                </button>
              </div>
            ) : areas.data.length === 0 ? (
              <div className="panel-state">
                <h3>Aún no hay áreas registradas</h3>
                <p>Registra un área para comenzar a usar el menú.</p>
              </div>
            ) : (
              <div className="inventory-area-grid">
                {areas.data.map((area) => (
                  <button
                    key={area.id}
                    type="button"
                    className="inventory-area-card"
                    onClick={() => void navigate(`/menu/areas/${area.id}`)}
                  >
                    <span className="inventory-area-icon">
                      <Utensils size={22} />
                    </span>
                    <strong>{area.name}</strong>
                    <span>Ver menú</span>
                    <ArrowRight size={18} aria-hidden="true" />
                  </button>
                ))}
              </div>
            )}
          </section>
          {unassigned.isError && (
            <section className="panel-state">
              <Notice>{menuErrorMessage(unassigned.error)}</Notice>
              <button className="button button-secondary" onClick={() => void unassigned.refetch()}>
                Reintentar
              </button>
            </section>
          )}
          {unassigned.data && unassigned.data.total > 0 && (
            <section className="panel">
              <div className="panel-toolbar">
                <div>
                  <h2>Productos pendientes de asignar</h2>
                  <p>Selecciona el área de cada producto registrado anteriormente.</p>
                </div>
              </div>
              {canManageMenu ? (
                unassigned.data.items.map((product) => (
                  <UnassignedProductRow
                    key={product.id}
                    product={product}
                    areas={areas.data ?? []}
                    onAssigned={() => saved("El producto se asignó al área seleccionada.")}
                  />
                ))
              ) : (
                <div className="panel-state">
                  Hay productos anteriores pendientes de asignación por un administrador o
                  supervisor.
                </div>
              )}
              {canManageMenu && (
                <Pagination
                  page={unassigned.data.page}
                  total={unassigned.data.total}
                  pageSize={unassigned.data.pageSize}
                  onChange={setUnassignedPage}
                />
              )}
            </section>
          )}
        </>
      )}
      {selectedAreaId && !selectedArea && areas.isPending && (
        <section className="panel-state" role="status">
          Cargando el área…
        </section>
      )}
      {selectedAreaId && !selectedArea && areas.isError && (
        <section className="panel-state">
          <Notice>{menuErrorMessage(areas.error)}</Notice>
          <button className="button button-secondary" onClick={() => void areas.refetch()}>
            Reintentar
          </button>
        </section>
      )}
      {selectedAreaId && areas.data && !selectedArea && (
        <section className="panel-state">
          <Notice>El área solicitada no está registrada.</Notice>
          <button className="button button-secondary" onClick={() => void navigate("/menu")}>
            Volver a las áreas
          </button>
        </section>
      )}
      {selectedAreaId && selectedArea && (
        <>
          <section className="panel menu-panel">
            <div className="panel-toolbar">
              <div>
                <h2>
                  <Utensils size={17} /> Productos del menú
                </h2>
                <p>Los precios se registran en pesos mexicanos y admiten dos decimales.</p>
              </div>
              <label className="search-input">
                <Search size={18} aria-hidden="true" />
                <input
                  type="search"
                  aria-label="Buscar producto del menú"
                  placeholder="Buscar código de barras, producto o categoría…"
                  maxLength={100}
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </label>
            </div>
            {products.isPending ? (
              <div className="panel-state" role="status">
                Cargando menú…
              </div>
            ) : products.isError ? (
              <div className="panel-state">
                <Notice>{menuErrorMessage(products.error)}</Notice>
                <button className="button button-secondary" onClick={() => void products.refetch()}>
                  Reintentar
                </button>
              </div>
            ) : products.data.items.length === 0 ? (
              <div className="panel-state">
                <span className="empty-icon">
                  <Coffee size={27} />
                </span>
                <h3>{debouncedSearch ? "No encontramos productos" : "El menú está vacío"}</h3>
                <p>
                  {debouncedSearch
                    ? "Prueba con otra clave, nombre o categoría."
                    : "Agrega alimentos o bebidas para formar el catálogo de venta."}
                </p>
                {!debouncedSearch && canManageMenu && (
                  <button
                    className="button button-primary"
                    onClick={() => setEditor({ product: null })}
                  >
                    <Plus size={17} /> Registrar primer producto
                  </button>
                )}
              </div>
            ) : (
              <>
                <div className="table-scroll">
                  <table className="data-table menu-table">
                    <thead>
                      <tr>
                        <th>Producto</th>
                        <th>Categoría</th>
                        <th>Descripción</th>
                        <th>Precio de venta</th>
                        <th>Estado</th>
                        {canManageMenu && (
                          <th>
                            <span className="sr-only">Acciones</span>
                          </th>
                        )}
                      </tr>
                    </thead>
                    <tbody>
                      {products.data.items.map((product) => (
                        <tr key={product.id}>
                          <td>
                            <div className="inventory-item-cell">
                              <span className="avatar">
                                <Utensils size={17} />
                              </span>
                              <div>
                                <strong>{product.name}</strong>
                                <span>{product.code}</span>
                              </div>
                            </div>
                          </td>
                          <td>{product.category}</td>
                          <td>
                            {product.description || <span className="muted">Sin descripción</span>}
                          </td>
                          <td>
                            <strong className="menu-price">{formatPrice(product.price)}</strong>
                          </td>
                          <td>
                            {product.isActive ? (
                              <span className="status status-active">Disponible</span>
                            ) : (
                              <span className="status status-neutral">Inactivo</span>
                            )}
                          </td>
                          {canManageMenu && (
                            <td>
                              <div className="row-actions">
                                <button
                                  className="icon-button inventory-toggle-button"
                                  data-active={product.isActive}
                                  title={
                                    product.isActive ? "Desactivar producto" : "Activar producto"
                                  }
                                  aria-label={`${product.isActive ? "Desactivar" : "Activar"} ${product.name}`}
                                  onClick={() => setStatusProduct(product)}
                                >
                                  <Power size={17} />
                                </button>
                                <button
                                  className="icon-button"
                                  title="Editar producto"
                                  aria-label={`Editar ${product.name}`}
                                  onClick={() => setEditor({ product })}
                                >
                                  <Pencil size={17} />
                                </button>
                              </div>
                            </td>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <Pagination
                  page={products.data.page}
                  total={products.data.total}
                  pageSize={products.data.pageSize}
                  onChange={setPage}
                />
              </>
            )}
          </section>
          <SecurityNote>
            El catálogo de menú no modifica las existencias del inventario. Los registros y cambios
            de precio quedan asociados a tu usuario.
          </SecurityNote>
          {editor && (
            <ProductEditor
              areaId={selectedAreaId}
              areaName={selectedArea.name}
              product={editor.product}
              onClose={() => setEditor(null)}
              onSaved={saved}
            />
          )}
          {statusProduct && (
            <ProductStatusDialog
              areaId={selectedAreaId}
              product={statusProduct}
              onClose={() => setStatusProduct(null)}
              onSaved={(isActive) =>
                saved(isActive ? "El producto fue activado." : "El producto fue desactivado.")
              }
            />
          )}
        </>
      )}
    </div>
  );
}
