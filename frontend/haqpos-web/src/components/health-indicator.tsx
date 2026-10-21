import { CircleCheck, CircleDashed, CircleX } from "lucide-react";
import { useHealth } from "@/hooks/use-health";

export function HealthIndicator() {
  const health = useHealth();
  const healthy = health.data?.status === "ok";
  const Icon = health.isPending ? CircleDashed : healthy ? CircleCheck : CircleX;
  const label = health.isPending
    ? "Comprobando API…"
    : healthy
      ? "API disponible"
      : "API no disponible";
  return (
    <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
      <Icon aria-hidden="true" className="size-4" />
      {label}
    </p>
  );
}
