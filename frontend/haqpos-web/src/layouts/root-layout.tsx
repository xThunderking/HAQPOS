import { useState } from "react";
import { Link, NavLink, Outlet, useLocation } from "react-router";
import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  Boxes,
  MapPinned,
  ReceiptText,
  ChevronRight,
  Coffee,
  LogOut,
  Menu,
  Store,
  ShieldCheck,
  UserRound,
  Users,
  X,
} from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { Brand, Notice } from "@/components/user-interface";
import { errorMessage } from "@/services/users";
import { areasApi } from "@/services/areas";

export function RootLayout() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const areaMatch = location.pathname.match(/^\/(inventario|menu)\/areas\/([^/]+)\/?$/);
  const areaId = areaMatch?.[2];
  const areas = useQuery({
    queryKey: ["area-options"],
    queryFn: ({ signal }) => areasApi.options(signal),
    enabled: Boolean(areaId),
  });
  const homePath =
    user?.role === "Administrador"
      ? "/usuarios"
      : user?.role === "Supervisor"
        ? "/inventario"
        : "/cajas";
  const moduleRoutes = [
    { path: "/cajas", label: "Cajas" },
    { path: "/cuentas-por-cobrar", label: "Cuentas por cobrar" },
    { path: "/areas", label: "Áreas" },
    { path: "/clientes", label: "Clientes" },
    { path: "/inventario", label: "Inventario" },
    { path: "/menu", label: "Menú" },
    { path: "/usuarios", label: "Control de usuarios" },
    { path: "/actividad", label: "Actividad de seguridad" },
  ];
  const currentModule = moduleRoutes.find(
    (route) => location.pathname === route.path || location.pathname.startsWith(`${route.path}/`),
  );
  const currentArea = areas.data?.find((area) => area.id === areaId);
  const breadcrumbs = [{ label: "HAQPOS", to: homePath }];
  if (
    areaId &&
    currentModule &&
    (currentModule.path === "/inventario" || currentModule.path === "/menu")
  ) {
    breadcrumbs.push({ label: currentModule.label, to: currentModule.path });
    breadcrumbs.push({ label: currentArea?.name ?? "Área", to: "" });
  } else if (currentModule) {
    breadcrumbs.push({ label: currentModule.label, to: "" });
  }
  const [menuOpen, setMenuOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function leave() {
    setBusy(true);
    setError("");
    try {
      await logout();
    } catch (failure) {
      setError(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="app-shell">
      {menuOpen && (
        <button
          className="sidebar-backdrop"
          aria-label="Cerrar menú"
          onClick={() => setMenuOpen(false)}
        />
      )}
      <aside className={`sidebar ${menuOpen ? "sidebar-open" : ""}`}>
        <div className="sidebar-brand">
          <Brand />
          <button
            className="icon-button mobile-only"
            aria-label="Cerrar menú"
            onClick={() => setMenuOpen(false)}
          >
            <X size={20} />
          </button>
        </div>
        <nav aria-label="Navegación principal">
          <span className="nav-section-label">OPERACIÓN</span>
          <NavLink to="/cajas" onClick={() => setMenuOpen(false)}>
            <Store size={19} /> Cajas
          </NavLink>
          <NavLink to="/cuentas-por-cobrar" onClick={() => setMenuOpen(false)}>
            <ReceiptText size={19} /> Cuentas por cobrar
          </NavLink>
          <NavLink to="/areas" onClick={() => setMenuOpen(false)}>
            <MapPinned size={19} /> Áreas
          </NavLink>
          <NavLink to="/clientes" onClick={() => setMenuOpen(false)}>
            <UserRound size={19} /> Clientes
          </NavLink>
          <NavLink to="/inventario" onClick={() => setMenuOpen(false)}>
            <Boxes size={19} /> Inventario
          </NavLink>
          <NavLink to="/menu" onClick={() => setMenuOpen(false)}>
            <Coffee size={19} /> Menú
          </NavLink>
          <span className="nav-section-label">ADMINISTRACIÓN</span>
          <NavLink to="/usuarios" onClick={() => setMenuOpen(false)}>
            <Users size={19} /> Control de usuarios
          </NavLink>
          <NavLink to="/actividad" onClick={() => setMenuOpen(false)}>
            <Activity size={19} /> Actividad de seguridad
          </NavLink>
        </nav>
        <div className="sidebar-bottom">
          <div className="sidebar-security">
            <ShieldCheck size={20} />
            <div>
              <strong>Sesión protegida</strong>
              <span>Tu acceso es personal</span>
            </div>
          </div>
          <button
            className="logout-button"
            disabled={busy}
            onClick={() => {
              void leave();
            }}
          >
            <LogOut size={18} />
            {busy ? "Cerrando sesión…" : "Cerrar sesión"}
          </button>
          <span className="sidebar-version">HAQPOS · Gestión hospitalaria</span>
        </div>
      </aside>
      <div className="app-workspace">
        <header className="topbar">
          <nav className="breadcrumb" aria-label="Ruta de navegación">
            <button
              className="icon-button mobile-only"
              aria-label="Abrir menú"
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen(true)}
            >
              <Menu size={21} />
            </button>
            {breadcrumbs.map((crumb, index) => (
              <span className="breadcrumb-part" key={`${crumb.label}-${index}`}>
                {index > 0 && <ChevronRight className="breadcrumb-separator" size={14} />}
                {crumb.to ? (
                  <Link
                    className="breadcrumb-link"
                    to={crumb.to}
                    aria-current={index === breadcrumbs.length - 1 ? "page" : undefined}
                  >
                    {crumb.label}
                  </Link>
                ) : (
                  <strong aria-current="page">{crumb.label}</strong>
                )}
              </span>
            ))}
          </nav>
          <div className="topbar-account">
            <span className="avatar avatar-small">{user?.fullName.slice(0, 1)}</span>
            <div>
              <strong>{user?.fullName}</strong>
              <span>
                {user?.role} · #{user?.employeeNumber}
              </span>
            </div>
          </div>
        </header>
        <main className="main-content">
          {error && <Notice>{error}</Notice>}
          <Outlet />
        </main>
        <footer className="workspace-footer">
          HAQPOS <span>Gestión hospitalaria con acceso seguro</span>
        </footer>
      </div>
    </div>
  );
}
