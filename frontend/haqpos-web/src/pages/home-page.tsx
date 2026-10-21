import { HealthIndicator } from "@/components/health-indicator";

export function HomePage() {
  return (
    <section className="w-full max-w-lg space-y-4 rounded-xl border bg-card p-8 text-card-foreground shadow-sm">
      <h1 className="text-3xl font-semibold tracking-tight">HAQPOS</h1>
      <p className="text-muted-foreground">Entorno de desarrollo configurado correctamente.</p>
      <HealthIndicator />
    </section>
  );
}
