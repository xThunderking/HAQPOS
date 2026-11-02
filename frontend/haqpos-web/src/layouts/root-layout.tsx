import { useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router";
import {
  Activity,
  Boxes,
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

export function RootLayout() {
  const { user, logout } = useAuth();
  const location = useLocation();
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
  const title =
    location.pathname === "/usuarios"
      ? "Control de usuarios"
      : location.pathname === "/actividad"
        ? "Actividad de seguridad"
        : location.pathname === "/inventario"
          ? "Inventario"
          : location.pathname === "/menu"
            ? "Menú"
            : location.pathname === "/cajas"
              ? "Cajas"
              : location.pathname === "/clientes"
                ? "Clientes"
                : "HAQPOS";
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
          <NavLink to="/clientes" onClick={() => setMenuOpen(false)}>
            <UserRound size={19} /> Clientes
          </NavLink>
          {(user?.role === "Administrador" || user?.role === "Supervisor") && (
            <>
              <NavLink to="/inventario" onClick={() => setMenuOpen(false)}>
                <Boxes size={19} /> Inventario
              </NavLink>
              <NavLink to="/menu" onClick={() => setMenuOpen(false)}>
                <Coffee size={19} /> Menú
              </NavLink>
            </>
          )}
          {user?.role === "Administrador" && (
            <>
              <span className="nav-section-label">ADMINISTRACIÓN</span>
              <NavLink to="/usuarios" onClick={() => setMenuOpen(false)}>
                <Users size={19} /> Control de usuarios
              </NavLink>
              <NavLink to="/actividad" onClick={() => setMenuOpen(false)}>
                <Activity size={19} /> Actividad de seguridad
              </NavLink>
            </>
          )}
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
          <div className="breadcrumb">
            <button
              className="icon-button mobile-only"
              aria-label="Abrir menú"
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen(true)}
            >
              <Menu size={21} />
            </button>
            <span>HAQPOS</span>
            <ChevronRight size={14} />
            <strong>{title}</strong>
          </div>
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
