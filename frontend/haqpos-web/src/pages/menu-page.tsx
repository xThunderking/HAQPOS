import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Coffee, Pencil, Plus, Power, Search, Utensils } from "lucide-react";
import { Modal, Notice, Pagination, SecurityNote } from "@/components/user-interface";
import { formatPrice, menuApi, menuErrorMessage, type MenuProduct } from "@/services/menu";

function ProductEditor({
  product,
  onClose,
  onSaved,
}: {
  product: MenuProduct | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [code, setCode] = useState(product?.code ?? "");
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
        code: code.trim(),
        name: name.trim(),
        category: category.trim(),
        description: description.trim() || null,
        price: Number(price),
      };
      if (product) await menuApi.update(product, common);
      else await menuApi.create(common);
      onSaved();
    } catch (failure) {
      setError(menuErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title={product ? "Editar producto" : "Registrar producto"}
      description="Este producto se ofrecerá en el menú y no descontará existencias del inventario."
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        <div className="inventory-form-grid">
          <label className="field">
            <span>Clave del producto</span>
            <input
              required
              maxLength={30}
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder="Ej. AGUA-JAMAICA"
            />
          </label>
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
  onClose,
  onSaved,
}: {
  product: MenuProduct;
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
      await menuApi.setActive(product, isActive);
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

export function MenuPage() {
  const client = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<{ product: MenuProduct | null } | null>(null);
  const [statusProduct, setStatusProduct] = useState<MenuProduct | null>(null);
  const [message, setMessage] = useState("");
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);
  const products = useQuery({
    queryKey: ["menu-products", debouncedSearch, page],
    queryFn: ({ signal }) => menuApi.list(debouncedSearch, page, signal),
  });

  function saved(success = "El producto se guardó correctamente.") {
    setEditor(null);
    setStatusProduct(null);
    setMessage(success);
    void client.invalidateQueries({ queryKey: ["menu-products"] });
  }

  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">PRODUCTOS DE VENTA</span>
          <h1>Menú</h1>
          <p>Administra alimentos y bebidas que no requieren control de existencias.</p>
        </div>
        <button
          className="button button-primary"
          onClick={() => {
            setEditor({ product: null });
            setMessage("");
          }}
        >
          <Plus size={18} /> Nuevo producto
        </button>
      </div>
      {message && <Notice success>{message}</Notice>}
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
              placeholder="Buscar clave, producto o categoría…"
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
            {!debouncedSearch && (
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
                    <th>
                      <span className="sr-only">Acciones</span>
                    </th>
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
                      <td>
                        <div className="row-actions">
                          <button
                            className="icon-button inventory-toggle-button"
                            data-active={product.isActive}
                            title={product.isActive ? "Desactivar producto" : "Activar producto"}
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
        El catálogo de menú no modifica las existencias del inventario. Los registros y cambios de
        precio quedan asociados a tu usuario.
      </SecurityNote>
      {editor && (
        <ProductEditor product={editor.product} onClose={() => setEditor(null)} onSaved={saved} />
      )}
      {statusProduct && (
        <ProductStatusDialog
          product={statusProduct}
          onClose={() => setStatusProduct(null)}
          onSaved={(isActive) =>
            saved(isActive ? "El producto fue activado." : "El producto fue desactivado.")
          }
        />
      )}
    </div>
  );
}
