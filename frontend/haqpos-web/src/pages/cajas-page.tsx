import { Store } from "lucide-react";

export function CajasPage() {
  return (
    <div className="page-stack">
      <div className="page-heading">
        <div>
          <span className="eyebrow">OPERACIÓN</span>
          <h1>Cajas</h1>
        </div>
      </div>
      <section className="panel-state">
        <span className="empty-icon">
          <Store size={28} />
        </span>
        <h2>Módulo vacío por ahora</h2>
        <p>Aún no hay contenido para mostrar en Cajas.</p>
      </section>
    </div>
  );
}
