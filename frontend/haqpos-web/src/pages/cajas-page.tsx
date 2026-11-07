import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CirclePlus,
  History,
  LockKeyhole,
  MapPinned,
  Plus,
  ReceiptText,
  RefreshCw,
  Store,
  X,
} from "lucide-react";
import { Modal, Notice, Pagination } from "@/components/user-interface";
import { CashAccountPanel } from "@/components/cash-account-panel";
import { CutDialog, NewAccountDialog } from "@/components/cash-dialogs";
import { useAuth } from "@/hooks/use-auth";
import { resolveCashCommands, useCashCommandId } from "@/hooks/use-cash-command";
import { areasApi } from "@/services/areas";
import { formatDate } from "@/services/users";
import { cashApi, cashError, folio, money, type CashAccount } from "@/services/cash";

export function CajasPage() {
  const { user } = useAuth();
  const client = useQueryClient();
  const workspace = useQuery({
    queryKey: ["cash-workspace"],
    queryFn: ({ signal }) => cashApi.workspace(signal),
  });
  const areas = useQuery({
    queryKey: ["area-options"],
    queryFn: ({ signal }) => areasApi.options(signal),
    enabled: user?.role === "Administrador" || user?.role === "Supervisor",
  });
  const [areaId, setAreaId] = useState("");
  const [opening, setOpening] = useState("0");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [status, setStatus] = useState<CashAccount["status"]>("Abierta");
  const [newAccount, setNewAccount] = useState(false);
  const [closeShift, setCloseShift] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyPage, setHistoryPage] = useState(1);
  const [cutId, setCutId] = useState<string | null>(null);
  const command = useCashCommandId();
  const history = useQuery({
    queryKey: ["cash-history", historyPage],
    queryFn: ({ signal }) => cashApi.history(historyPage, signal),
    enabled: historyOpen,
  });
  const shift = workspace.data?.shift;
  useEffect(() => {
    if (shift) resolveCashCommands([shift.id]);
  }, [shift]);
  const activeAccount =
    selected ??
    workspace.data?.accounts.find((a) => a.status === "Abierta")?.id ??
    workspace.data?.accounts[0]?.id;
  function refresh() {
    for (const key of [
      "cash-workspace",
      "cash-history",
      "cash-receivables",
      "cash-account",
      "cash-products",
      "inventory",
      "inventory-movements",
      "audit",
      "clients",
    ])
      void client.invalidateQueries({ queryKey: [key] });
  }
  async function open(event: FormEvent) {
    event.preventDefault();
    if (busy || !workspace.data) return;
    setBusy(true);
    setError("");
    const area = user?.role === "Cajero" ? (workspace.data.assignedAreaId ?? "") : areaId;
    try {
      await cashApi.openShift(
        command.for(JSON.stringify([user?.id, area, opening])),
        area,
        Number(opening),
      );
      command.clear();
      setSelected(null);
      refresh();
    } catch (failure) {
      setError(cashError(failure));
    } finally {
      setBusy(false);
    }
  }
  async function close() {
    if (!shift || busy) return;
    setBusy(true);
    setError("");
    try {
      const result = await cashApi.closeShift(shift.id);
      setCloseShift(false);
      setCutId(result.id);
      setSelected(null);
      refresh();
    } catch (failure) {
      setError(cashError(failure));
      refresh();
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="page-stack cash-page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">PUNTO DE VENTA</span>
          <h1>Cajas</h1>
          {shift ? (
            <p className="cash-shift-caption">
              <span>Caja #{folio(shift.number)}</span>
              <span className="cash-shift-caption-separator" aria-hidden="true">·</span>
              <span className="cash-shift-area">{shift.areaName}</span>
              <span className="cash-shift-caption-separator" aria-hidden="true">·</span>
              <span>{shift.cashierName}</span>
            </p>
          ) : (
            <p>Abre tu turno para administrar cuentas y registrar cobros.</p>
          )}
        </div>
        <div className="page-heading-actions">
          <button
            className="icon-button"
            title="Actualizar caja"
            aria-label="Actualizar caja"
            disabled={workspace.isFetching}
            onClick={refresh}
          >
            <RefreshCw size={18} />
          </button>
          <button className="button button-secondary" onClick={() => setHistoryOpen(!historyOpen)}>
            <History size={17} /> Cortes anteriores
          </button>
          {shift && (
            <>
              <button
                className="button button-secondary"
                onClick={() => {
                  setError("");
                  setCloseShift(true);
                }}
              >
                <LockKeyhole size={17} /> Cerrar caja
              </button>
              <button className="button button-primary" onClick={() => setNewAccount(true)}>
                <Plus size={18} /> Nueva cuenta
              </button>
            </>
          )}
        </div>
      </div>
      {error && <Notice>{error}</Notice>}
      {workspace.isPending ? (
        <section className="panel-state" role="status">
          Cargando tu caja…
        </section>
      ) : workspace.isError ? (
        <section className="panel-state">
          <Notice>{cashError(workspace.error)}</Notice>
          <button className="button button-secondary" onClick={() => void workspace.refetch()}>
            Reintentar
          </button>
        </section>
      ) : !shift ? (
        <section className="panel cash-open-panel">
          <span className="cash-open-icon">
            <Store size={32} />
          </span>
          <h2>Comienza un nuevo turno</h2>
          <p>Cada cuenta y movimiento quedarán asociados a tu caja y al área de operación.</p>
          {!workspace.data.canOperate ? (
            <Notice>
              {user?.role === "Cajero"
                ? "Tu usuario no tiene un área asignada. Solicita la asignación a Sistemas para operar la caja."
                : "Tu rol puede consultar este módulo. La operación de caja está disponible para cajeros, supervisores y administradores."}
            </Notice>
          ) : (
            <form className="form-stack" onSubmit={(e) => void open(e)}>
              <label className="field">
                <span>Área de operación</span>
                {user?.role === "Cajero" ? (
                  <div className="cash-area-assignment">
                    <MapPinned size={18} />
                    <strong>{workspace.data.assignedAreaName}</strong>
                  </div>
                ) : (
                  <select required value={areaId} onChange={(e) => setAreaId(e.target.value)}>
                    <option value="">Selecciona un área</option>
                    {areas.data?.map((area) => (
                      <option key={area.id} value={area.id}>
                        {area.name}
                      </option>
                    ))}
                  </select>
                )}
              </label>
              {areas.isError && <Notice>{cashError(areas.error)}</Notice>}
              <label className="field">
                <span>Fondo de apertura (MXN)</span>
                <input
                  type="number"
                  inputMode="decimal"
                  min="0"
                  max="999999999.99"
                  step="0.01"
                  required
                  value={opening}
                  onChange={(e) => setOpening(e.target.value)}
                />
              </label>
              <button className="button button-primary" disabled={busy}>
                <Store size={18} /> {busy ? "Abriendo…" : "Abrir caja"}
              </button>
            </form>
          )}
        </section>
      ) : (
        <>
          <div className="cash-workspace">
            <aside className="panel cash-accounts">
              <div className="cash-accounts-heading">
                <h2>Cuentas del turno</h2>
                <CirclePlus size={20} />
              </div>
              <div className="cash-account-tabs" role="group" aria-label="Filtrar cuentas">
                {(["Abierta", "Pagada", "Pendiente", "Cancelada"] as const).map((value) => (
                  <button
                    key={value}
                    className={status === value ? "selected" : ""}
                    onClick={() => setStatus(value)}
                  >
                    {value === "Abierta"
                      ? "Abiertas"
                      : value === "Pagada"
                        ? "Cerradas"
                        : value === "Pendiente"
                          ? "CXC pendientes"
                          : "Canceladas"}
                  </button>
                ))}
              </div>
              <div className="cash-accounts-list">
                {workspace.data?.accounts
                  .filter((a) => a.status === status)
                  .map((account) => (
                    <button
                      key={account.id}
                      className={`cash-account-card ${activeAccount === account.id ? "selected" : ""}`}
                      onClick={() => setSelected(account.id)}
                    >
                      <span>
                        #{folio(account.number)}
                        <small>{formatDate(account.openedAt)}</small>
                      </span>
                      <strong>{account.clientName}</strong>
                      <span>
                        #{account.registrationNumber}
                        <b>{money(account.total)}</b>
                      </span>
                    </button>
                  ))}
                {!workspace.data?.accounts.some((a) => a.status === status) && (
                  <div className="cash-list-empty">
                    <ReceiptText size={25} />
                    <p>
                      No hay cuentas{" "}
                      {status === "Abierta"
                        ? "abiertas"
                        : status === "Pagada"
                          ? "cerradas"
                          : status === "Pendiente"
                            ? "pendientes CXC"
                            : "canceladas"}
                      .
                    </p>
                    {status === "Abierta" && (
                      <button
                        className="button button-secondary button-small"
                        onClick={() => setNewAccount(true)}
                      >
                        Abrir cuenta
                      </button>
                    )}
                  </div>
                )}
              </div>
            </aside>
            {activeAccount ? (
              <CashAccountPanel
                key={activeAccount}
                id={activeAccount}
                shift={shift}
                onChanged={refresh}
              />
            ) : (
              <section className="panel cash-select-account">
                <ReceiptText size={44} />
                <h2>Tu punto de venta está listo</h2>
                <p>
                  Abre una cuenta para agregar productos. Puedes mantener varias cuentas abiertas y
                  cambiar entre ellas.
                </p>
                <button className="button button-primary" onClick={() => setNewAccount(true)}>
                  <Plus size={18} /> Abrir primera cuenta
                </button>
              </section>
            )}
          </div>
        </>
      )}
      {historyOpen && (
        <section className="panel">
          <div className="panel-toolbar">
            <div>
              <h2>Cortes anteriores</h2>
              <p>
                {user?.role === "Administrador" || user?.role === "Supervisor"
                  ? "Consulta los cortes cerrados de los cajeros."
                  : "Consulta y vuelve a descargar tus cortes de caja."}
              </p>
            </div>
            <button
              className="icon-button"
              aria-label="Ocultar cortes anteriores"
              onClick={() => setHistoryOpen(false)}
            >
              <X size={19} />
            </button>
          </div>
          {history.isPending ? (
            <div className="panel-state" role="status">
              Cargando cortes…
            </div>
          ) : history.isError ? (
            <Notice>{cashError(history.error)}</Notice>
          ) : history.data.items.length === 0 ? (
            <div className="panel-state">Todavía no hay cortes registrados.</div>
          ) : (
            <>
              <div className="table-scroll">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Corte / área</th>
                      <th>Cajero</th>
                      <th>Cierre</th>
                      <th>Total registrado</th>
                      <th>Recibo</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.data.items.map((item) => (
                      <tr key={item.id}>
                        <td>
                          <strong>#{folio(item.number)}</strong>
                          <small>{item.areaName}</small>
                        </td>
                        <td>{item.cashierName}</td>
                        <td>{formatDate(item.closedAt!)}</td>
                        <td>{money(item.total)}</td>
                        <td>
                          <button
                            className="button button-secondary button-small"
                            onClick={() => setCutId(item.id)}
                          >
                            <ReceiptText size={16} /> Ver corte
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination
                page={history.data.page}
                total={history.data.total}
                pageSize={history.data.pageSize}
                onChange={setHistoryPage}
              />
            </>
          )}
        </section>
      )}
      {newAccount && shift && (
        <NewAccountDialog
          shift={shift}
          onClose={() => setNewAccount(false)}
          onSaved={(id) => {
            setNewAccount(false);
            setSelected(id);
            setStatus("Abierta");
            refresh();
          }}
        />
      )}
      {closeShift && shift && (
        <Modal
          title="Cerrar caja y generar corte"
          description={`Caja #${folio(shift.number)} · ${shift.areaName}`}
          busy={busy}
          onClose={() => setCloseShift(false)}
        >
          <div className="form-stack">
            {error && <Notice>{error}</Notice>}
            {shift.openAccounts > 0 ? (
              <Notice>
                Quedan {shift.openAccounts} cuentas abiertas. Cóbralas o cancélalas antes de cerrar
                caja.
              </Notice>
            ) : (
              <>
                <p>
                  Se cerrará tu turno con {shift.paidAccounts} cuentas cobradas o de cortesía,{" "}
                  {shift.pendingAccounts} pendientes CXC y {shift.cancelledAccounts} canceladas.
                </p>
                <div className="cash-payment-total">
                  <span>Total registrado</span>
                  <strong>{money(shift.total)}</strong>
                </div>
                <p>Después podrás descargar el recibo de corte en PDF o abrirlo para imprimir.</p>
              </>
            )}
            <div className="modal-actions">
              <button
                className="button button-secondary"
                disabled={busy}
                onClick={() => setCloseShift(false)}
              >
                Volver
              </button>
              <button
                className="button button-primary"
                disabled={busy || shift.openAccounts > 0}
                onClick={() => void close()}
              >
                {busy ? "Cerrando…" : "Cerrar caja"}
              </button>
            </div>
          </div>
        </Modal>
      )}
      {cutId && <CutDialog id={cutId} onClose={() => setCutId(null)} />}
    </div>
  );
}
