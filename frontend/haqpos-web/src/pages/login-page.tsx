import { useState, type FormEvent } from "react";
import { ArrowRight, ShieldCheck, UserRound } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { Brand, Notice, PasswordField } from "@/components/user-interface";
import { usersApi, errorMessage } from "@/services/users";

export function LoginPage() {
  const { setUser } = useAuth();
  const [employee, setEmployee] = useState("");
  const [password, setPassword] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setPending(true);
    try {
      const result = await usersApi.login(employee.trim(), password);
      setPassword("");
      setUser(result.user);
    } catch (failure) {
      setError(errorMessage(failure));
      setPassword("");
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="login-shell">
      <section className="login-card">
        <div className="login-brand-wrap">
          <Brand />
        </div>
        <header className="login-heading">
          <span className="login-kicker">
            <ShieldCheck size={14} aria-hidden="true" /> Acceso seguro
          </span>
          <h1>Inicia sesión</h1>
          <p>Ingresa con tus credenciales de empleado.</p>
        </header>
        <form
          onSubmit={(event) => {
            void submit(event);
          }}
          className="form-stack login-form"
        >
          {error && <Notice>{error}</Notice>}
          <label className="field">
            <span>Número de empleado</span>
            <span className="login-input-wrap">
              <UserRound size={17} aria-hidden="true" />
              <input
                name="username"
                autoComplete="username"
                inputMode="numeric"
                pattern="[0-9]{1,20}"
                maxLength={20}
                required
                value={employee}
                onChange={(event) => setEmployee(event.target.value)}
                disabled={pending}
              />
            </span>
          </label>
          <PasswordField
            label="Contraseña"
            name="password"
            autoComplete="current-password"
            required
            maxLength={128}
            value={password}
            disabled={pending}
            onChange={(event) => setPassword(event.target.value)}
          />
          <button
            className="button button-primary button-block"
            disabled={pending}
            aria-busy={pending}
          >
            {pending ? "Verificando acceso…" : "Ingresar"}
            <ArrowRight size={18} />
          </button>
        </form>
      </section>
    </main>
  );
}