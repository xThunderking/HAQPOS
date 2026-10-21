# Estructura completa de archivos

Excluye Git interno, herramientas/secretos locales, dependencias y resultados de compilación.

```text
HAQPOS/
├── .config/
│   └── dotnet-tools.json
├── backend/
│   ├── HAQPOS.Api/
│   │   ├── Errors/
│   │   │   └── GlobalExceptionHandler.cs
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.Development.json
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   ├── HAQPOS.Api.csproj
│   │   ├── packages.lock.json
│   │   └── Program.cs
│   ├── HAQPOS.Application/
│   │   ├── HAQPOS.Application.csproj
│   │   └── packages.lock.json
│   ├── HAQPOS.Domain/
│   │   ├── HAQPOS.Domain.csproj
│   │   └── packages.lock.json
│   └── HAQPOS.Infrastructure/
│       ├── Persistence/
│       │   └── HaqposDbContext.cs
│       ├── DependencyInjection.cs
│       ├── HAQPOS.Infrastructure.csproj
│       └── packages.lock.json
├── docs/
│   ├── ARCHITECTURE.md
│   ├── DEPLOYMENT_UBUNTU.md
│   ├── DEVELOPMENT.md
│   └── PROJECT_STRUCTURE.md
├── frontend/
│   └── haqpos-web/
│       ├── public/
│       │   └── robots.txt
│       ├── src/
│       │   ├── app/
│       │   │   ├── app.tsx
│       │   │   └── query-client.ts
│       │   ├── components/
│       │   │   └── health-indicator.tsx
│       │   ├── hooks/
│       │   │   └── use-health.ts
│       │   ├── layouts/
│       │   │   └── root-layout.tsx
│       │   ├── lib/
│       │   │   └── utils.ts
│       │   ├── pages/
│       │   │   └── home-page.tsx
│       │   ├── services/
│       │   │   ├── health.ts
│       │   │   ├── http-client.test.ts
│       │   │   └── http-client.ts
│       │   ├── styles/
│       │   │   └── globals.css
│       │   ├── main.tsx
│       │   └── vite-env.d.ts
│       ├── .env.example
│       ├── .gitignore
│       ├── .node-version
│       ├── .npmrc
│       ├── components.json
│       ├── index.html
│       ├── package.json
│       ├── pnpm-lock.yaml
│       ├── pnpm-workspace.yaml
│       ├── tsconfig.app.json
│       ├── tsconfig.json
│       ├── tsconfig.node.json
│       └── vite.config.ts
├── infrastructure/
│   ├── docker/
│   │   ├── api.Dockerfile
│   │   ├── compose.postgres.yaml
│   │   ├── compose.yaml
│   │   └── frontend.Dockerfile
│   ├── nginx/
│   │   └── haqpos.conf
│   └── scripts/
│       ├── Initialize-DevelopmentHttps.ps1
│       ├── Start-LocalPostgres.ps1
│       └── Use-LocalTools.ps1
├── tests/
│   ├── HAQPOS.IntegrationTests/
│   │   ├── HAQPOS.IntegrationTests.csproj
│   │   ├── HealthTests.cs
│   │   └── packages.lock.json
│   └── HAQPOS.UnitTests/
│       ├── HAQPOS.UnitTests.csproj
│       ├── packages.lock.json
│       └── PersistenceConfigurationTests.cs
├── .dockerignore
├── .editorconfig
├── .env.example
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── HAQPOS.slnx
├── NuGet.Config
└── README.md
```
