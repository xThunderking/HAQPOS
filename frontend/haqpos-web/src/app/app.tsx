import { QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes } from "react-router";
import { queryClient } from "@/app/query-client";
import { RootLayout } from "@/layouts/root-layout";
import { CajasPage } from "@/pages/cajas-page";
import { AreasPage } from "@/pages/areas-page";
import { ClientesPage } from "@/pages/clientes-page";
import { AuthProvider } from "@/app/auth";
import { useAuth } from "@/hooks/use-auth";
import { LoginPage } from "@/pages/login-page";
import { PasswordPage } from "@/pages/password-page";
import { UsersPage } from "@/pages/users-page";
import { AuditPage } from "@/pages/audit-page";
import { InventoryPage } from "@/pages/inventory-page";
import { MenuPage } from "@/pages/menu-page";
import { Brand, Notice } from "@/components/user-interface";

function ApplicationRoutes() {
  const { user, loading, error, refresh } = useAuth();
  if (loading)
    return (
      <main className="session-loading">
        <Brand />
        <p role="status">Comprobando tu sesiÃ³nâ€¦</p>
      </main>
    );
  if (error)
    return (
      <main className="session-loading">
        <Brand />
        <Notice>
          No fue posible conectar con el sistema. Comprueba tu conexiÃ³n e intÃ©ntalo de nuevo.
        </Notice>
        <button
          className="button button-primary"
          onClick={() => {
            void refresh();
          }}
        >
          Reintentar conexiÃ³n
        </button>
      </main>
    );
  if (!user)
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    );
  if (user.mustChangePassword)
    return (
      <Routes>
        <Route path="/actualizar-contrasena" element={<PasswordPage required />} />
        <Route path="*" element={<Navigate to="/actualizar-contrasena" replace />} />
      </Routes>
    );
  return (
    <Routes>
      <Route element={<RootLayout />}>
        <Route path="/cajas" element={<CajasPage />} />
        <Route path="/areas" element={<AreasPage />} />
        <Route path="/clientes" element={<ClientesPage />} />
        <Route path="/inventario" element={<InventoryPage />} />
        <Route path="/menu" element={<MenuPage />} />
        <Route path="/usuarios" element={<UsersPage />} />
        <Route path="/actividad" element={<AuditPage />} />
        <Route
          path="*"
          element={
            <Navigate
              to={
                user.role === "Administrador"
                  ? "/usuarios"
                  : user.role === "Supervisor"
                    ? "/inventario"
                    : "/cajas"
              }
              replace
            />
          }
        />
      </Route>
    </Routes>
  );
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <ApplicationRoutes />
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
