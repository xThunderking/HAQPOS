import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Search, Trash2, UserRound } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { Modal, Notice, Pagination } from "@/components/user-interface";
import { clientErrorMessage, clientsApi, type ClientRecord } from "@/services/clients";

function ClientEditor({
  client,
  onClose,
  onSaved,
}: {
  client: ClientRecord | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [registrationNumber, setRegistrationNumber] = useState(client?.registrationNumber ?? "");
  const [fullName, setFullName] = useState(client?.fullName ?? "");
  const [category, setCategory] = useState<"NORMAL" | "CORTESIA" | "CONVENIO">(
    client?.category ?? "NORMAL",
  );
  const [isExternal, setIsExternal] = useState(client?.isExternal ?? false);
  const [office, setOffice] = useState(client?.office ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const input = {
        registrationNumber: registrationNumber.trim(),
        fullName: fullName.trim(),
        category,
        isExternal,
        office: isExternal ? null : office.trim(),
      };
      if (client) await clientsApi.update(client.id, { ...input, version: client.version });
      else await clientsApi.create(input);
      onSaved();
    } catch (failure) {
      setError(clientErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title={client ? "Editar cliente" : "Registrar cliente"}
      description={
        client
          ? "Actualiza los datos del médico o colaborador."
          : "Registra los datos del médico o colaborador."
      }
      busy={busy}
      onClose={onClose}
    >
      <form className="form-stack" onSubmit={(event) => void submit(event)}>
        {error && <Notice>{error}</Notice>}
        <label className="field">
          <span>Número GASS / No. de colaborador</span>
          <input
            type="text"
            autoComplete="off"
            maxLength={10}
            required
            value={registrationNumber}
            onChange={(event) =>
              setRegistrationNumber(
                event.target.value
                  .replace(/[^a-zA-Z0-9]/g, "")
                  .slice(0, 10)
                  .toUpperCase(),
              )
            }
            placeholder="Máximo 10 caracteres"
          />
          <small>Ingresa de 1 a 10 letras o números. Se conservarán los ceros iniciales.</small>
        </label>
        <label className="field">
          <span>Nombre completo del médico o colaborador</span>
          <input
            type="text"
            autoComplete="name"
            minLength={3}
            maxLength={150}
            required
            value={fullName}
            onChange={(event) => setFullName(event.target.value.replace(/[\p{N}]/gu, ""))}
            placeholder="Nombre y apellidos"
          />
        </label>
        <label className="field">
          <span>Categoría</span>
          <select
            required
            value={category}
            onChange={(event) =>
              setCategory(event.target.value as "NORMAL" | "CORTESIA" | "CONVENIO")
            }
          >
            <option value="NORMAL">Normal</option>
            <option value="CORTESIA">Cortesía</option>
            <option value="CONVENIO">Convenio</option>
          </select>
        </label>
        <label className="field">
          <span>Consultorio / Externo</span>
          <select
            value={isExternal ? "externo" : "consultorio"}
            onChange={(event) => {
              const external = event.target.value === "externo";
              setIsExternal(external);
              if (external) setOffice("");
            }}
          >
            <option value="consultorio">Consultorio</option>
            <option value="externo">Externo</option>
          </select>
        </label>
        {!isExternal && (
          <label className="field">
            <span>Consultorio del médico</span>
            <input
              type="text"
              minLength={2}
              maxLength={80}
              required
              value={office}
              onChange={(event) => setOffice(event.target.value)}
              placeholder="Número o ubicación del consultorio"
            />
          </label>
        )}
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
            {busy ? "Guardando…" : client ? "Guardar cambios" : "Registrar cliente"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function DeleteClientConfirmation({
  client,
  onClose,
  onDeleted,
}: {
  client: ClientRecord;
  onClose: () => void;
  onDeleted: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function remove() {
    setBusy(true);
    setError("");
    try {
      await clientsApi.delete(client.id, client.version);
      onDeleted();
    } catch (failure) {
      setError(clientErrorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      title="Eliminar cliente"
      description={`¿Deseas eliminar a ${client.fullName} (#${client.registrationNumber})? Esta acción no se puede deshacer.`}
      busy={busy}
      onClose={onClose}
    >
      <div className="form-stack">
        {error && <Notice>{error}</Notice>}
        <div className="modal-actions">
          <button
            type="button"
            className="button button-secondary"
            disabled={busy}
            onClick={onClose}
          >
            Cancelar
          </button>
          <button
            type="button"
            className="button button-danger"
            disabled={busy}
            onClick={() => void remove()}
          >
            {busy ? "Eliminando…" : "Eliminar cliente"}
          </button>
        </div>
      </div>
    </Modal>
  );
}

export function ClientesPage() {
  const { user } = useAuth();
  const canManageClients = user?.role !== "Consultor" && user?.role !== "Almacenista";
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editorClient, setEditorClient] = useState<ClientRecord | null | undefined>(undefined);
  const [deleteClient, setDeleteClient] = useState<ClientRecord | null>(null);
  const [message, setMessage] = useState("");

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  const clients = useQuery({
    queryKey: ["clients", debouncedSearch, page],
    queryFn: ({ signal }) => clientsApi.list(debouncedSearch, page, signal),
  });

  async function saved(wasEdit: boolean) {
    setEditorClient(undefined);
    setMessage(
      wasEdit ? "Los datos del cliente se actualizaron." : "El cliente se registró correctamente.",
    );
    await queryClient.invalidateQueries({ queryKey: ["clients"] });
    await queryClient.invalidateQueries({ queryKey: ["audit"] });
  }

  async function deleted() {
    setDeleteClient(null);
    setMessage("El cliente se eliminó correctamente.");
    await queryClient.invalidateQueries({ queryKey: ["clients"] });
    await queryClient.invalidateQueries({ queryKey: ["audit"] });
  }

  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">OPERACIÓN</span>
          <h1>Clientes</h1>
          <p>Registra y consulta médicos y colaboradores.</p>
        </div>
        {canManageClients && (
          <button
            className="button button-primary"
            onClick={() => {
              setMessage("");
              setEditorClient(null);
            }}
          >
            <Plus size={18} /> Registrar cliente
          </button>
        )}
      </div>
      {message && <Notice success>{message}</Notice>}
      <section className="panel">
        <div className="panel-toolbar">
          <div>
            <h2>Directorio de clientes</h2>
            <p>Busca por número, nombre o consultorio.</p>
          </div>
          <label className="search-input">
            <Search size={18} aria-hidden="true" />
            <input
              type="search"
              aria-label="Buscar cliente por número, nombre o consultorio"
              placeholder="Buscar cliente…"
              maxLength={100}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
        </div>
        {clients.isPending ? (
          <div className="panel-state" role="status">
            Cargando clientes…
          </div>
        ) : clients.isError ? (
          <div className="panel-state">
            <Notice>{clientErrorMessage(clients.error)}</Notice>
            <button className="button button-secondary" onClick={() => void clients.refetch()}>
              Reintentar
            </button>
          </div>
        ) : clients.data.items.length === 0 ? (
          <div className="panel-state">
            <span className="empty-icon">
              <UserRound size={28} />
            </span>
            <h3>{debouncedSearch ? "No encontramos clientes" : "Aún no hay clientes"}</h3>
            <p>
              {debouncedSearch
                ? "Prueba con otro número, nombre o consultorio."
                : "Registra al primer médico o colaborador para comenzar el directorio."}
            </p>
            {!debouncedSearch && canManageClients && (
              <button className="button button-primary" onClick={() => setEditorClient(null)}>
                <Plus size={17} /> Registrar primer cliente
              </button>
            )}
          </div>
        ) : (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Número GASS / colaborador</th>
                    <th>Nombre completo</th>
                    <th>Categoría</th>
                    <th>Consultorio / Externo</th>
                    {canManageClients && <th>Acciones</th>}
                  </tr>
                </thead>
                <tbody>
                  {clients.data.items.map((client) => (
                    <tr key={client.id}>
                      <td>#{client.registrationNumber}</td>
                      <td>{client.fullName}</td>
                      <td>
                        {client.category === "CORTESIA"
                          ? "Cortesía"
                          : client.category === "CONVENIO"
                            ? "Convenio"
                            : "Normal"}
                      </td>
                      <td>{client.isExternal ? "Externo" : client.office}</td>
                      {canManageClients && (
                        <td>
                          <div className="row-actions">
                            <button
                              className="icon-button"
                              title="Editar cliente"
                              aria-label={`Editar a ${client.fullName}`}
                              onClick={() => setEditorClient(client)}
                            >
                              <Pencil size={17} />
                            </button>
                            <button
                              className="icon-button"
                              title="Eliminar cliente"
                              aria-label={`Eliminar a ${client.fullName}`}
                              onClick={() => setDeleteClient(client)}
                            >
                              <Trash2 size={17} />
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
              page={clients.data.page}
              total={clients.data.total}
              pageSize={clients.data.pageSize}
              onChange={setPage}
            />
          </>
        )}
      </section>
      {editorClient !== undefined && (
        <ClientEditor
          key={editorClient?.id ?? "new-client"}
          client={editorClient}
          onClose={() => setEditorClient(undefined)}
          onSaved={() => void saved(editorClient !== null)}
        />
      )}
      {deleteClient && (
        <DeleteClientConfirmation
          client={deleteClient}
          onClose={() => setDeleteClient(null)}
          onDeleted={() => void deleted()}
        />
      )}
    </div>
  );
}
