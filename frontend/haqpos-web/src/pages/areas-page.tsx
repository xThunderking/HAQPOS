import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { MapPinned, Plus, Search } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { Modal, Notice, Pagination } from "@/components/user-interface";
import { areaErrorMessage, areasApi } from "@/services/areas";

function AreaEditor({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await areasApi.create({ name: name.trim(), description: description.trim() });
      onSaved();
    } catch (failure) {
      setError(areaErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title="Registrar área"
      description="Ingresa el nombre y la descripción del área."
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        <label className="field">
          <span>Nombre</span>
          <input
            type="text"
            autoComplete="off"
            minLength={2}
            maxLength={100}
            required
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Nombre del área"
          />
        </label>
        <label className="field">
          <span>Descripción</span>
          <textarea
            minLength={3}
            maxLength={500}
            required
            rows={4}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Describe brevemente el área"
          />
        </label>
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
            {busy ? "Guardando…" : "Registrar área"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

export function AreasPage() {
  const { user } = useAuth();
  const canManageAreas = user?.role !== "Consultor" && user?.role !== "Almacenista";
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editorOpen, setEditorOpen] = useState(false);
  const [message, setMessage] = useState("");

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  const areas = useQuery({
    queryKey: ["areas", debouncedSearch, page],
    queryFn: ({ signal }) => areasApi.list(debouncedSearch, page, signal),
  });

  async function saved() {
    setEditorOpen(false);
    setMessage("El área se registró correctamente.");
    await queryClient.invalidateQueries({ queryKey: ["areas"] });
    await queryClient.invalidateQueries({ queryKey: ["area-options"] });
    await queryClient.invalidateQueries({ queryKey: ["audit"] });
  }

  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">OPERACIÓN</span>
          <h1>Áreas</h1>
          <p>Registra y consulta las áreas del hospital.</p>
        </div>
        {canManageAreas && (
          <button
            className="button button-primary"
            onClick={() => {
              setMessage("");
              setEditorOpen(true);
            }}
          >
            <Plus size={18} /> Registrar área
          </button>
        )}
      </div>
      {message && <Notice success>{message}</Notice>}
      <section className="panel">
        <div className="panel-toolbar">
          <div>
            <h2>Directorio de áreas</h2>
            <p>Consulta las áreas registradas en el sistema.</p>
          </div>
          <label className="search-input">
            <Search size={18} aria-hidden="true" />
            <input
              type="search"
              aria-label="Buscar área por nombre o descripción"
              placeholder="Buscar área…"
              maxLength={100}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
        </div>
        {areas.isPending ? (
          <div className="panel-state" role="status">
            Cargando áreas…
          </div>
        ) : areas.isError ? (
          <div className="panel-state">
            <Notice>{areaErrorMessage(areas.error)}</Notice>
            <button className="button button-secondary" onClick={() => void areas.refetch()}>
              Reintentar
            </button>
          </div>
        ) : areas.data.items.length === 0 ? (
          <div className="panel-state">
            <span className="empty-icon">
              <MapPinned size={28} />
            </span>
            <h3>{debouncedSearch ? "No encontramos áreas" : "Aún no hay áreas"}</h3>
            <p>
              {debouncedSearch
                ? "Prueba con otro nombre o descripción."
                : "Registra la primera área para comenzar el directorio."}
            </p>
            {!debouncedSearch && canManageAreas && (
              <button className="button button-primary" onClick={() => setEditorOpen(true)}>
                <Plus size={17} /> Registrar primera área
              </button>
            )}
          </div>
        ) : (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Nombre</th>
                    <th>Descripción</th>
                  </tr>
                </thead>
                <tbody>
                  {areas.data.items.map((area) => (
                    <tr key={area.id}>
                      <td>{area.name}</td>
                      <td>{area.description}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={areas.data.page}
              total={areas.data.total}
              pageSize={areas.data.pageSize}
              onChange={setPage}
            />
          </>
        )}
      </section>
      {editorOpen && (
        <AreaEditor onClose={() => setEditorOpen(false)} onSaved={() => void saved()} />
      )}
    </div>
  );
}
