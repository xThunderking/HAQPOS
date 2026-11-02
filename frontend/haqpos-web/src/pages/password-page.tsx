import { useState, type FormEvent } from "react";
import { Check, KeyRound, LogOut } from "lucide-react";
import { useNavigate } from "react-router";
import { useAuth } from "@/hooks/use-auth";
import { Brand, Notice, PasswordField, SecurityNote } from "@/components/user-interface";
import { usersApi, errorMessage } from "@/services/users";

export function PasswordPage({ required = false }: { required?: boolean }) {
  const { user, setUser, logout, refresh } = useAuth();
  const navigate = useNavigate();
  const [current, setCurrent] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setSuccess(false);
    if (password !== confirm) {
      setError("Las contraseñas nuevas no coinciden.");
      return;
    }
    setBusy(true);
    try {
      const result = await usersApi.changePassword(current, password, confirm);
      setCurrent("");
      setPassword("");
      setConfirm("");
      setUser(result.user);
      if (required)
        await navigate(
          result.user.role === "Administrador"
            ? "/usuarios"
            : result.user.role === "Supervisor"
              ? "/inventario"
              : "/cajas",
          { replace: true },
        );
      else setSuccess(true);
    } catch (failure) {
      setError(errorMessage(failure));
      setCurrent("");
      await refresh();
    } finally {
      setBusy(false);
    }
  }
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
  const content = (
    <section className="password-card">
      <div className="section-icon">
        <KeyRound size={25} />
      </div>
      <span className="eyebrow">{required ? "PRIMER INGRESO" : "SEGURIDAD DE TU CUENTA"}</span>
      <h1>{required ? "Crea tu propia contraseña" : "Cambiar contraseña"}</h1>
      <p className="muted">
        {required
          ? `${user?.fullName ?? ""}, antes de continuar, reemplaza tu contraseña temporal por una que solo tú conozcas.`
          : "Al actualizarla, se cerrarán tus otras sesiones de forma automática."}
      </p>
      <form
        className="form-stack"
        onSubmit={(event) => {
          void submit(event);
        }}
      >
        {error && <Notice>{error}</Notice>}
        {success && (
          <Notice success>Contraseña actualizada. Tus otras sesiones se cerraron.</Notice>
        )}
        <PasswordField
          label={required ? "Contraseña temporal" : "Contraseña actual"}
          name="current-password"
          autoComplete="current-password"
          required
          maxLength={128}
          disabled={busy}
          value={current}
          onChange={(event) => setCurrent(event.target.value)}
        />
        <PasswordField
          label="Nueva contraseña"
          name="new-password"
          autoComplete="new-password"
          required
          minLength={15}
          maxLength={128}
          disabled={busy}
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          hint="De 15 a 128 caracteres. Puedes usar una frase con espacios."
        />
        <PasswordField
          label="Confirmar nueva contraseña"
          name="confirm-password"
          autoComplete="new-password"
          required
          minLength={15}
          maxLength={128}
          disabled={busy}
          value={confirm}
          onChange={(event) => setConfirm(event.target.value)}
        />
        <div className="password-checks">
          <span className={password.length >= 15 ? "complete" : ""}>
            <Check size={15} /> Al menos 15 caracteres
          </span>
          <span className={password.length > 0 && password === confirm ? "complete" : ""}>
            <Check size={15} /> Las contraseñas coinciden
          </span>
        </div>
        <button className="button button-primary button-block" disabled={busy}>
          {busy
            ? "Actualizando…"
            : required
              ? "Actualizar y continuar"
              : "Guardar nueva contraseña"}
        </button>
      </form>
      <SecurityNote>
        Evita tu número de empleado y contraseñas comunes. Nunca compartas tu contraseña personal.
      </SecurityNote>
      {required && (
        <button
          className="button button-plain button-block"
          disabled={busy}
          onClick={() => {
            void leave();
          }}
        >
          <LogOut size={16} /> Salir de esta cuenta
        </button>
      )}
    </section>
  );
  return required ? (
    <main className="first-login">
      <Brand />
      {content}
      <p className="muted small">
        Empleado {user?.employeeNumber} · Acceso limitado hasta actualizar la contraseña
      </p>
    </main>
  ) : (
    content
  );
}
