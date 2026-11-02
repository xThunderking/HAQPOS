import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Check,
  Copy,
  KeyRound,
  Pencil,
  Plus,
  Search,
  Unlock,
  UserRound,
  Users,
} from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { Modal, Notice, Pagination, SecurityNote } from "@/components/user-interface";
import {
  errorMessage,
  formatDate,
  roles,
  usersApi,
  type TemporaryCredentials,
  type User,
  type UserRole,
} from "@/services/users";

function UserStatus({ user, now }: { user: User; now: number }) {
  if (!user.isActive) return <span className="status status-neutral">Inactivo</span>;
  if (user.lockedUntil && Date.parse(user.lockedUntil) > now)
    return <span className="status status-danger">Bloqueado</span>;
  if (user.mustChangePassword)
    return (
      <span className="status status-warning">
        {user.temporaryPasswordExpiresAt && Date.parse(user.temporaryPasswordExpiresAt) <= now
          ? "Temporal caducada"
          : "Cambio pendiente"}
      </span>
    );
  return <span className="status status-active">Activo</span>;
}

function UserEditor({
  user,
  onClose,
  onCreated,
  onUpdated,
}: {
  user: User | null;
  onClose: () => void;
  onCreated: (credentials: TemporaryCredentials) => void;
  onUpdated: () => void;
}) {
  const { user: current } = useAuth();
  const [employee, setEmployee] = useState(user?.employeeNumber ?? "");
  const [name, setName] = useState(user?.fullName ?? "");
  const [role, setRole] = useState<UserRole>(user?.role ?? "Cajero");
  const [active, setActive] = useState(user?.isActive ?? true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const ownAccount = user?.id === current?.id;
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      if (user) {
        await usersApi.update(user, name, role, active);
        onUpdated();
      } else onCreated(await usersApi.create(employee.trim(), name.trim(), role));
    } catch (failure) {
      setError(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title={user ? "Editar usuario" : "Registrar usuario"}
      description={
        user
          ? "Actualiza su información y permisos de acceso."
          : "El número de empleado será su usuario para ingresar."
      }
      busy={busy}
      onClose={onClose}
    >
      <form
        className="form-stack"
        onSubmit={(event) => {
          void submit(event);
        }}
      >
        {error && <Notice>{error}</Notice>}
        <label className="field">
          <span>Número de empleado</span>
          <input
            name="employee-number"
            inputMode="numeric"
            pattern="[0-9]{1,20}"
            maxLength={20}
            required
            value={employee}
            disabled={busy || Boolean(user)}
            onChange={(event) => setEmployee(event.target.value)}
            placeholder="Ej. 50052143"
          />
          {user && <small>El número de empleado es único y no se modifica.</small>}
        </label>
        <label className="field">
          <span>Nombre completo</span>
          <input
            name="full-name"
            autoComplete="name"
            required
            minLength={3}
            maxLength={150}
            value={name}
            disabled={busy}
            onChange={(event) => setName(event.target.value)}
            placeholder="Nombre y apellidos"
          />
        </label>
        <label className="field">
          <span>Rol de acceso</span>
          <select
            value={role}
            onChange={(event) => setRole(event.target.value as UserRole)}
            disabled={busy || ownAccount}
          >
            {roles.map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
        {user ? (
          <label className="switch-field">
            <input
              type="checkbox"
              checked={active}
              disabled={busy || ownAccount}
              onChange={(event) => setActive(event.target.checked)}
            />
            <span>
              <strong>Cuenta activa</strong>
              <small>
                {ownAccount
                  ? "Tu cuenta administradora debe permanecer activa."
                  : "Al desactivarla se cerrarán todas sus sesiones."}
              </small>
            </span>
          </label>
        ) : (
          <SecurityNote>
            Se generará una contraseña temporal válida por 24 horas. El usuario tendrá que cambiarla
            en su primer ingreso.
          </SecurityNote>
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
            {busy ? "Guardando…" : user ? "Guardar cambios" : "Crear y generar contraseña"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function CredentialsDialog({
  credentials,
  onClose,
}: {
  credentials: TemporaryCredentials;
  onClose: () => void;
}) {
  const [copied, setCopied] = useState(false);
  const [copyError, setCopyError] = useState("");
  async function copy() {
    try {
      await navigator.clipboard.writeText(credentials.temporaryPassword);
      setCopied(true);
      setCopyError("");
    } catch {
      setCopyError("No se pudo copiar. Selecciona la contraseña y cópiala manualmente.");
    }
  }
  return (
    <Modal
      title="Contraseña temporal generada"
      description="Se muestra únicamente en este momento."
      onClose={onClose}
    >
      <div className="credentials-person">
        <span className="avatar">
          <UserRound size={20} />
        </span>
        <div>
          <strong>{credentials.user.fullName}</strong>
          <span>
            Empleado {credentials.user.employeeNumber} · {credentials.user.role}
          </span>
        </div>
      </div>
      <div className="credential-box">
        <span>CONTRASEÑA TEMPORAL</span>
        <code>{credentials.temporaryPassword}</code>
        <button
          className="button button-secondary button-block"
          onClick={() => {
            void copy();
          }}
        >
          {copied ? <Check size={17} /> : <Copy size={17} />}
          {copied ? "Copiada" : "Copiar contraseña"}
        </button>
      </div>
      {copyError && <Notice>{copyError}</Notice>}
      <p className="muted small">
        Válida hasta {formatDate(credentials.expiresAt)}. El acceso inicial solo permite crear una
        contraseña personal.
      </p>
      <SecurityNote>
        Entrégala a la persona indicada por un canal seguro. Si la pierde, genera una nueva desde su
        cuenta.
      </SecurityNote>
      <div className="modal-actions">
        <button className="button button-primary" onClick={onClose}>
          Entendido, cerrar
        </button>
      </div>
    </Modal>
  );
}

function ResetDialog({
  user,
  onClose,
  onSuccess,
}: {
  user: User;
  onClose: () => void;
  onSuccess: (credentials: TemporaryCredentials) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function reset() {
    setBusy(true);
    setError("");
    try {
      onSuccess(await usersApi.reset(user.id));
    } catch (failure) {
      setError(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title="Restablecer contraseña"
      description={`Empleado ${user.employeeNumber} · ${user.fullName}`}
      busy={busy}
      onClose={onClose}
    >
      <p>
        Se generará una nueva contraseña temporal, se desbloqueará la cuenta y se cerrarán todas sus
        sesiones. La contraseña anterior dejará de funcionar.
      </p>
      {error && <Notice>{error}</Notice>}
      <div className="modal-actions">
        <button className="button button-secondary" disabled={busy} onClick={onClose}>
          Cancelar
        </button>
        <button
          className="button button-primary"
          disabled={busy}
          onClick={() => {
            void reset();
          }}
        >
          {busy ? "Generando…" : "Generar nueva contraseña"}
        </button>
      </div>
    </Modal>
  );
}

function UnlockDialog({
  user,
  onClose,
  onSuccess,
}: {
  user: User;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function unlock() {
    setBusy(true);
    setError("");
    try {
      await usersApi.unlock(user.id);
      onSuccess();
    } catch (failure) {
      setError(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title="Desbloquear cuenta"
      description={`Empleado ${user.employeeNumber} · ${user.fullName}`}
      busy={busy}
      onClose={onClose}
    >
      <p>Se quitará el bloqueo por intentos fallidos. La contraseña no cambiará.</p>
      {error && <Notice>{error}</Notice>}
      <SecurityNote>Confirma que la persona solicitante es titular de esta cuenta.</SecurityNote>
      <div className="modal-actions">
        <button className="button button-secondary" disabled={busy} onClick={onClose}>
          Cancelar
        </button>
        <button
          className="button button-primary"
          disabled={busy}
          onClick={() => {
            void unlock();
          }}
        >
          {busy ? "Desbloqueando…" : "Confirmar desbloqueo"}
        </button>
      </div>
    </Modal>
  );
}

export function UsersPage() {
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => window.clearInterval(timer);
  }, []);
  const { user: current, refresh } = useAuth();
  const client = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<{ user: User | null } | null>(null);
  const [resetUser, setResetUser] = useState<User | null>(null);
  const [unlockUser, setUnlockUser] = useState<User | null>(null);
  const [credentials, setCredentials] = useState<TemporaryCredentials | null>(null);
  const [message, setMessage] = useState("");
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);
  const users = useQuery({
    queryKey: ["users", debouncedSearch, page],
    queryFn: ({ signal }) => usersApi.list(debouncedSearch, page, signal),
  });
  async function reload() {
    await Promise.all([
      client.invalidateQueries({ queryKey: ["users"] }),
      client.invalidateQueries({ queryKey: ["audit"] }),
    ]);
  }
  function received(value: TemporaryCredentials) {
    setEditor(null);
    setResetUser(null);
    setCredentials(value);
    setMessage("");
    void reload();
  }
  function updated() {
    setEditor(null);
    setMessage("Los cambios del usuario se guardaron correctamente.");
    void reload();
    void refresh();
  }
  function unlocked() {
    setUnlockUser(null);
    setMessage("La cuenta fue desbloqueada correctamente.");
    void reload();
  }
  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ADMINISTRACIÓN</span>
          <h1>Control de usuarios</h1>
          <p>Gestiona a tu equipo y sus permisos de acceso al sistema.</p>
        </div>
        <button
          className="button button-primary"
          onClick={() => {
            setEditor({ user: null });
            setMessage("");
          }}
        >
          <Plus size={18} /> Registrar usuario
        </button>
      </div>
      {message && <Notice success>{message}</Notice>}
      <section className="panel users-panel">
        <div className="panel-toolbar">
          <div>
            <h2>Equipo de trabajo</h2>
            <p>El número de empleado es el identificador de acceso.</p>
          </div>
          <label className="search-input">
            <Search size={18} aria-hidden="true" />
            <input
              type="search"
              aria-label="Buscar por nombre o número de empleado"
              placeholder="Buscar nombre o empleado…"
              maxLength={150}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
        </div>
        {users.isPending ? (
          <div className="panel-state" role="status">
            Cargando usuarios…
          </div>
        ) : users.isError ? (
          <div className="panel-state">
            <Notice>{errorMessage(users.error)}</Notice>
            <button
              className="button button-secondary"
              onClick={() => {
                void users.refetch();
              }}
            >
              Reintentar
            </button>
          </div>
        ) : users.data.items.length === 0 ? (
          <div className="panel-state">
            <span className="empty-icon">
              <Users size={28} />
            </span>
            <h3>{debouncedSearch ? "No encontramos coincidencias" : "Tu equipo comienza aquí"}</h3>
            <p>
              {debouncedSearch
                ? "Prueba con otro nombre o número de empleado."
                : "Registra a la primera persona de tu equipo."}
            </p>
          </div>
        ) : (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Usuario</th>
                    <th>Rol de acceso</th>
                    <th>Estado</th>
                    <th>Último ingreso</th>
                    <th>
                      <span className="sr-only">Acciones</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {users.data.items.map((person) => (
                    <tr key={person.id}>
                      <td>
                        <div className="person-cell">
                          <span className="avatar">{person.fullName.slice(0, 1)}</span>
                          <div>
                            <strong>
                              {person.fullName}
                              {person.id === current?.id && <span className="you-label">Tú</span>}
                            </strong>
                            <span>#{person.employeeNumber}</span>
                          </div>
                        </div>
                      </td>
                      <td>
                        <span className={`role-label role-${person.role.toLowerCase()}`}>
                          {person.role}
                        </span>
                      </td>
                      <td>
                        <UserStatus user={person} now={now} />
                      </td>
                      <td className="date-cell">{formatDate(person.lastLoginAt)}</td>
                      <td>
                        <div className="row-actions">
                          {person.id !== current?.id &&
                            person.isActive &&
                            person.lockedUntil !== null &&
                            Date.parse(person.lockedUntil) > now && (
                              <button
                                className="icon-button"
                                title="Desbloquear cuenta"
                                aria-label={`Desbloquear la cuenta de ${person.fullName}`}
                                onClick={() => setUnlockUser(person)}
                              >
                                <Unlock size={17} />
                              </button>
                            )}
                          <button
                            className="icon-button"
                            title="Editar usuario"
                            aria-label={`Editar a ${person.fullName}`}
                            onClick={() => {
                              setEditor({ user: person });
                              setMessage("");
                            }}
                          >
                            <Pencil size={17} />
                          </button>
                          <button
                            className="icon-button"
                            title={
                              person.id === current?.id
                                ? "No puedes restablecer tu propia contraseña"
                                : "Restablecer contraseña"
                            }
                            aria-label={`Restablecer contraseña de ${person.fullName}`}
                            disabled={person.id === current?.id || !person.isActive}
                            onClick={() => setResetUser(person)}
                          >
                            <KeyRound size={17} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={users.data.page}
              total={users.data.total}
              pageSize={users.data.pageSize}
              onChange={setPage}
            />
          </>
        )}
      </section>
      <SecurityNote>
        Las altas, los cambios de permisos y los restablecimientos de contraseña quedan registrados
        en la actividad de seguridad.
      </SecurityNote>
      {editor && (
        <UserEditor
          user={editor.user}
          onClose={() => setEditor(null)}
          onCreated={received}
          onUpdated={updated}
        />
      )}
      {resetUser && (
        <ResetDialog user={resetUser} onClose={() => setResetUser(null)} onSuccess={received} />
      )}
      {unlockUser && (
        <UnlockDialog user={unlockUser} onClose={() => setUnlockUser(null)} onSuccess={unlocked} />
      )}
      {credentials && (
        <CredentialsDialog credentials={credentials} onClose={() => setCredentials(null)} />
      )}
    </div>
  );
}
