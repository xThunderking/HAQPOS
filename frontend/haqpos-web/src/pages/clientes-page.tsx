import { UserRound } from "lucide-react";

export function ClientesPage() {
  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">OPERACIÓN</span>
          <h1>Clientes</h1>
        </div>
      </div>
      <section className="panel-state">
        <span className="empty-icon">
          <UserRound size={28} />
        </span>
        <h2>Módulo vacío por ahora</h2>
        <p>Aún no hay contenido para mostrar en Clientes.</p>
      </section>
    </div>
  );
}
