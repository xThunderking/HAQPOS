import { existsSync, readFileSync } from "node:fs";
import { Agent } from "node:https";
import { fileURLToPath, URL } from "node:url";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig, lazyPlugins, loadEnv } from "vite-plus";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const certificatePath = fileURLToPath(
    new URL("../../.tools/certs/localhost.pem", import.meta.url),
  );
  const keyPath = fileURLToPath(new URL("../../.tools/certs/localhost.key", import.meta.url));
  const certificate =
    existsSync(certificatePath) && existsSync(keyPath)
      ? { cert: readFileSync(certificatePath), key: readFileSync(keyPath) }
      : undefined;

  return {
    plugins: lazyPlugins(() => [react(), tailwindcss()]),
    resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
    server: {
      host: "localhost",
      port: Number(env.FRONTEND_PORT || 5173),
      strictPort: true,
      https: certificate,
      proxy: {
        "/api": {
          target: env.API_PROXY_TARGET || "https://localhost:7143",
          changeOrigin: true,
          secure: true,
          agent: certificate ? new Agent({ ca: certificate.cert }) : undefined,
        },
      },
    },
    test: { environment: "node", include: ["src/**/*.test.{ts,tsx}"] },
    fmt: {},
    lint: {
      plugins: ["react", "typescript", "oxc"],
      options: { typeAware: true, typeCheck: true },
      rules: {
        "react/rules-of-hooks": "error",
        "react/only-export-components": ["warn", { allowConstantExport: true }],
        "vite-plus/prefer-vite-plus-imports": "error",
      },
      jsPlugins: [{ name: "vite-plus", specifier: "vite-plus/oxlint-plugin" }],
    },
  };
});
