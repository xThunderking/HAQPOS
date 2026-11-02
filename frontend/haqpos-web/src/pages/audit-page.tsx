import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Activity, RefreshCw } from "lucide-react";
import { Notice, Pagination } from "@/components/user-interface";
import { errorMessage, formatDate, usersApi } from "@/services/users";

const labels: Record<string, string> = {
  "installation.admin_created": "Administrador inicial creado",
  "installation.admin_recovery": "Administrador recuperado desde el servidor",
  "user.created": "Usuario registrado",
  "user.locked": "Cuenta bloqueada por intentos fallidos",
  "user.unlocked": "Cuenta desbloqueada por administrador",
  "user.updated": "Información o permisos actualizados",
  "password.reset": "Contraseña restablecida",
  "password.changed": "Contraseña personal actualizada",
  "password.change_failed": "Intento de cambio de contraseña rechazado",
  "auth.login_failed": "Intento de acceso rechazado",
  "auth.login_succeeded": "Inicio de sesión correcto",
  "auth.logout": "Sesión cerrada",
  "inventory.item_created": "Insumo registrado",
  "inventory.item_updated": "Datos del insumo actualizados",
  "inventory.item_activated": "Insumo activado",
  "inventory.item_deactivated": "Insumo desactivado",
  "inventory.entrada": "Entrada de inventario registrada",
  "inventory.salida": "Salida de inventario registrada",
  "inventory.ajuste": "Existencia ajustada",
  "menu.product_created": "Producto de menú registrado",
  "menu.product_updated": "Producto de menú actualizado",
  "menu.product_activated": "Producto de menú activado",
  "menu.product_deactivated": "Producto de menú desactivado",
};
export function AuditPage() {
  const [page, setPage] = useState(1);
  const audit = useQuery({
    queryKey: ["audit", page],
    queryFn: ({ signal }) => usersApi.audit(page, signal),
  });
  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">TRAZABILIDAD</span>
          <h1>Actividad de seguridad</h1>
          <p>Consulta los accesos y los cambios realizados en las cuentas.</p>
        </div>
        <button
          className="button button-secondary"
          disabled={audit.isFetching}
          onClick={() => {
            void audit.refetch();
          }}
        >
          <RefreshCw size={16} /> Actualizar
        </button>
      </div>
      <section className="panel">
        <div className="panel-toolbar">
          <div>
            <h2>
              <Activity size={18} /> Registro de actividad
            </h2>
            <p>Los eventos se muestran del más reciente al más antiguo.</p>
          </div>
        </div>
        {audit.isPending ? (
          <div className="panel-state" role="status">
            Cargando actividad…
          </div>
        ) : audit.isError ? (
          <div className="panel-state">
            <Notice>{errorMessage(audit.error)}</Notice>
          </div>
        ) : (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Fecha y hora</th>
                    <th>Acción</th>
                    <th>Realizado por</th>
                    <th>Cuenta relacionada</th>
                  </tr>
                </thead>
                <tbody>
                  {audit.data.items.map((event) => (
                    <tr key={event.id}>
                      <td className="date-cell">{formatDate(event.occurredAt)}</td>
                      <td>{labels[event.action] ?? event.action}</td>
                      <td>
                        {event.actorEmployeeNumber
                          ? `#${event.actorEmployeeNumber}`
                          : "Sistema / sin sesión"}
                      </td>
                      <td>
                        {event.subjectEmployeeNumber ? `#${event.subjectEmployeeNumber}` : "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {audit.data.items.length === 0 && (
              <div className="panel-state">Todavía no hay actividad.</div>
            )}
            <Pagination
              page={audit.data.page}
              total={audit.data.total}
              pageSize={audit.data.pageSize}
              onChange={setPage}
            />
          </>
        )}
      </section>
    </div>
  );
}
