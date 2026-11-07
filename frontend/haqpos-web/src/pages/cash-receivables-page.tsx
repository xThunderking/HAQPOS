import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ReceiptText, Search } from "lucide-react";
import { Notice, Pagination } from "@/components/user-interface";
import { useDebouncedSearch } from "@/hooks/use-cash-command";
import { cashApi, cashError, folio, money } from "@/services/cash";
import { formatDate } from "@/services/users";

export function CashReceivablesPage() {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const debounced = useDebouncedSearch(search);
  const receivables = useQuery({
    queryKey: ["cash-receivables", page, debounced],
    queryFn: ({ signal }) => cashApi.receivables(page, debounced, signal),
  });

  return (
    <div className="page-stack cash-page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">SEGUIMIENTO DE VENTAS</span>
          <h1>Cuentas por cobrar</h1>
          <p>Consulta las cuentas liberadas como CXC que siguen pendientes de pago.</p>
        </div>
      </div>

      {receivables.isPending ? (
        <section className="panel panel-state" role="status">
          Cargando cuentas pendientes…
        </section>
      ) : receivables.isError ? (
        <Notice>{cashError(receivables.error)}</Notice>
      ) : (
        <>
          <section className="cash-receivables-summary">
            <div className="cash-receivables-summary-icon">
              <ReceiptText size={24} />
            </div>
            <div>
              <span>Saldo pendiente</span>
              <strong>{money(receivables.data.totalAmount)}</strong>
            </div>
            <div className="cash-receivables-count">
              <span>Cuentas pendientes</span>
              <strong>{receivables.data.total}</strong>
            </div>
          </section>

          <section className="panel">
            <div className="panel-toolbar">
              <div>
                <h2>Movimientos CXC</h2>
                <p>Las cuentas quedan de solo lectura en caja y conservan el detalle de consumo.</p>
              </div>
              <label className="cash-receivables-search" aria-label="Buscar cuentas por cobrar">
                <Search size={18} />
                <input
                  value={search}
                  maxLength={100}
                  onChange={(event) => {
                    setSearch(event.target.value);
                    setPage(1);
                  }}
                  placeholder="Cliente, GASS, área o cajero"
                />
              </label>
            </div>
            {receivables.data.items.length === 0 ? (
              <div className="panel-state">
                {debounced
                  ? "No hay cuentas pendientes que coincidan con la búsqueda."
                  : "No hay cuentas CXC pendientes."}
              </div>
            ) : (
              <>
                <div className="table-scroll">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Cuenta / cliente</th>
                        <th>Área y corte</th>
                        <th>Cajero</th>
                        <th>Consumo</th>
                        <th>Fecha</th>
                        <th>Saldo</th>
                      </tr>
                    </thead>
                    <tbody>
                      {receivables.data.items.map((item) => (
                        <tr key={item.account.id}>
                          <td>
                            <strong>
                              #{folio(item.account.number)} · {item.account.clientName}
                            </strong>
                            <small>GASS / colaborador #{item.account.registrationNumber}</small>
                          </td>
                          <td>
                            {item.areaName}
                            <small>Corte #{folio(item.shiftNumber)}</small>
                          </td>
                          <td>
                            {item.cashierName}
                            <small>Empleado #{item.employeeNumber}</small>
                          </td>
                          <td className="cash-receivable-products">
                            {item.lines
                              .filter((line) => !line.removedAt)
                              .map((line) => (
                                <span key={line.id}>
                                  {line.quantity} × {line.name}
                                </span>
                              ))}
                          </td>
                          <td>{formatDate(item.account.closedAt!)}</td>
                          <td>
                            <strong>{money(item.account.total)}</strong>
                            <small>Pendiente</small>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <Pagination
                  page={receivables.data.page}
                  total={receivables.data.total}
                  pageSize={receivables.data.pageSize}
                  onChange={setPage}
                />
              </>
            )}
          </section>
        </>
      )}
    </div>
  );
}
