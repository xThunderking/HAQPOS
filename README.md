# HAQPOS

Base técnica inicial para un futuro sistema de punto de venta de hospital privado. Incluye arquitectura, proyectos, dependencias, configuración y verificaciones técnicas. **No se implementó ningún módulo de negocio.**

Visual Studio 2026 administra la solución .NET; **Vite+** es la herramienta principal del frontend. No se necesita Docker para desarrollar frontend y API en Windows.

## Versiones comprobadas

| Tecnología | Versión |
| --- | --- |
| SDK .NET portable / C# | 10.0.402 / 14 |
| ASP.NET Core, EF Core, OpenAPI, dotnet-ef | 10.0.13 |
| Npgsql y proveedor EF PostgreSQL | 10.0.3 |
| Serilog.AspNetCore | 10.0.0 |
| Node existente / pnpm fijado | 22.23.2 / 10.32.1 |
| Vite+ local | **1.3.0 estable** |
| Vite / Rolldown integrados | 8.3.4 / 1.2.14 |
| Vitest / Oxlint / Oxfmt integrados | 5.0.5 / 1.88.0 / 0.73.0 |
| React / React DOM | 19.3.1 |
| TypeScript estricto | 6.0.3 |
| Plugin React | 6.1.2 |
| Tailwind CSS y plugin oficial | 4.3.3 |
| React Router / TanStack Query | 8.5.0 / 5.104.5 |
| Lucide React | 1.61.0 |
| shadcn/ui | Inicialización manual, estilo new-york; componentes por código, sin paquete runtime propio |
| clsx / tailwind-merge / tw-animate-css | 2.1.1 / 3.7.0 / 1.4.0 |
| PostgreSQL portable local | **18.6** |
| xUnit / adaptador Visual Studio / Test SDK | 2.9.3 / 3.1.5 / 18.11.0 |
| Docker CLI / Compose disponibles | 29.7.2 / 5.4.0 |

Los lockfiles fijan las dependencias transitivas. Imágenes **configuradas**, no ejecutadas: SDK 10.0.402, ASP.NET Core 10.0.13, Vite+ 1.3.0, PostgreSQL 18.6-bookworm y Nginx 1.30.2-alpine.

## Vite+

El paquete local `vite-plus` integra Vite 8, Rolldown, Vitest, Oxlint y Oxfmt. La configuración usa `defineConfig` de `vite-plus`; `vp check` reúne formato, lint y tipos. No hay ESLint, Prettier ni Vitest independiente. El archivo pnpm-workspace.yaml solo alinea dependencias mediante overrides; no define un monorepo.

Se consultaron la [guía oficial](https://viteplus.dev/guide/), [create](https://viteplus.dev/guide/create), [check](https://viteplus.dev/guide/check), [CLI local](https://viteplus.dev/guide/local-cli) y [Docker](https://viteplus.dev/guide/docker). El frontend se generó mediante `vp create vite:application` con `-- --template react-ts`.

Instalación global oficial **opcional**, documentada y no ejecutada en esta preparación:

```powershell
irm https://vite.plus/ps1 | iex
```

Es un instalador remoto: ejecútalo solo si decides instalar el CLI global. Después abre una terminal nueva y usa `vp help`, `vp create --list` y `vp toolchain --global`. Este último requiere el CLI global; para el instalado localmente usa `vp toolchain`.

## Estructura

```text
HAQPOS/
├── HAQPOS.slnx
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── NuGet.Config
├── .config/dotnet-tools.json
├── .gitignore, .editorconfig, .dockerignore, .env.example
├── backend/
│   ├── HAQPOS.Api/
│   ├── HAQPOS.Application/
│   ├── HAQPOS.Domain/
│   └── HAQPOS.Infrastructure/
├── frontend/haqpos-web/
│   ├── public/
│   ├── src/
│   │   ├── app/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── layouts/
│   │   ├── lib/
│   │   ├── pages/
│   │   ├── services/
│   │   └── styles/
│   ├── package.json, pnpm-lock.yaml, pnpm-workspace.yaml
│   ├── vite.config.ts, components.json
│   └── tsconfig.json, tsconfig.app.json, tsconfig.node.json
├── tests/
│   ├── HAQPOS.UnitTests/
│   └── HAQPOS.IntegrationTests/
├── infrastructure/
│   ├── docker/
│   ├── nginx/
│   └── scripts/
└── docs/
    ├── ARCHITECTURE.md
    ├── DEVELOPMENT.md
    ├── DEPLOYMENT_UBUNTU.md
    └── PROJECT_STRUCTURE.md
```

[Árbol completo de archivos](docs/PROJECT_STRUCTURE.md). Solo existe un repositorio Git en la raíz.

## Inicio rápido en este equipo

En cada terminal PowerShell, desde la raíz, activa las herramientas locales. El script no realiza instalaciones:

```powershell
. ./infrastructure/scripts/Use-LocalTools.ps1
```

Antes de usar el navegador, confía en el certificado de desarrollo; Windows puede mostrar una confirmación:

```powershell
./infrastructure/scripts/Initialize-DevelopmentHttps.ps1 -Trust
```

El certificado ya fue generado y exportado, pero no se instaló confianza de navegador durante la preparación.

Inicia PostgreSQL portable y la API:

```powershell
./infrastructure/scripts/Start-LocalPostgres.ps1
dotnet run --project backend/HAQPOS.Api --launch-profile https
```

En otra terminal, después de activar las herramientas:

```powershell
Set-Location frontend/haqpos-web
vp install --frozen-lockfile
vp dev
```

Abre **https://localhost:5173**. La API responde en **https://localhost:7143/api/health**. Vite+ proxifica /api al backend y verifica su certificado.

La base local es PostgreSQL 18.6 en **127.0.0.1:55432**, base `haqpos`, sin tablas públicas. La conexión privada ya se guardó en **User Secrets**. El PostgreSQL 16 preexistente permanece separado.

Para un clon nuevo instala las herramientas indicadas y configura PostgreSQL 18 nativo o el contenedor opcional. Los binarios portables y secretos en .tools están ignorados y no viajan en Git. Consulta [DEVELOPMENT.md](docs/DEVELOPMENT.md) para configurar la cadena externa, cambiar puertos y usar Docker solo para la base.

## Visual Studio 2026

Instala la carga **ASP.NET y desarrollo web** y el SDK 10.0.402. Abre `HAQPOS.slnx`, establece `HAQPOS.Api` como proyecto de inicio, selecciona el perfil **https** y pulsa F5. El frontend se inicia por separado mediante `vp dev`.

La solución contiene los seis proyectos y fue validada por `dotnet sln list`, compilación y pruebas. **No se verificó la apertura de la interfaz de Visual Studio.**

## Compilación y pruebas

Desde la raíz, con la API detenida:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet tool restore

Set-Location frontend/haqpos-web
vp install --frozen-lockfile
vp check
vp test run
vp build
```

`vp test` está disponible; `run` evita el modo observación. dotnet-ef 10.0.13 queda restaurado para trabajo futuro. No se crearon ni aplicaron migraciones.

### Resultados reales de preparación: 21 de octubre de 2026

| Verificación | Resultado |
| --- | --- |
| Restore .NET | Correcto; descarga inicial mediante intermediario local por limitación TLS del sandbox |
| Build solución completa | **Correcto: 0 advertencias, 0 errores** |
| Pruebas .NET | **3 aprobadas**: 1 unitaria, 2 de integración |
| Herramienta dotnet-ef | Restaurada, versión 10.0.13 |
| vp install y lockfile congelado | Correctos |
| vp check | **Correcto: sin errores de lint/tipos ni advertencias** |
| vp test run | **2 pruebas aprobadas** |
| vp build | **Correcto**, SPA generada en dist |
| vp dev y navegador Edge | Pantalla visible; sin errores JavaScript |
| API directa y proxy Vite+ | **HTTPS 200**, estado ok |
| PostgreSQL / EF Core | **18.6 operativo**, conexión correcta, cero tablas públicas |
| Docker Compose | **Ambos archivos válidos** |
| Docker build, Ubuntu y Visual Studio UI | Pendientes; no ejecutados |

El sandbox impidió conexiones TLS nativas y acceso a claves de Windows. Se resolvió la descarga con un intermediario temporal en loopback que verificaba HTTPS de los registros oficiales con Python. Ese intermediario no forma parte de la aplicación ni de sus manifiestos. Se confirmó después la restauración desde la caché con lockfiles y la compilación/pruebas mediante la configuración normal; los detalles constan en [DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Ubuntu Server y Docker

Ya están preparados los dos Dockerfiles multietapa, Nginx con TLS, Compose, redes internas, health checks y volumen de PostgreSQL 18. El frontend final sirve archivos estáticos desde Nginx. La base no publica puertos en producción y HTTPS se enlaza por defecto a loopback.

```bash
cp .env.example .env
# Definir contraseña, host interno y rutas de certificados de TI.
docker compose --env-file .env -f infrastructure/docker/compose.yaml config --quiet
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d --build
```

Los archivos no se desplegaron: falta iniciar Docker, construir las imágenes y suministrar certificados internos de TI. La aplicación no se expuso a internet ni a la LAN. [Procedimiento completo para Ubuntu](docs/DEPLOYMENT_UBUNTU.md).

[Arquitectura y decisiones técnicas](docs/ARCHITECTURE.md).
