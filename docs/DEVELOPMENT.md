# Desarrollo en Windows

## Requisitos y entorno actual

Para un clon nuevo: Visual Studio 2026 con la carga **ASP.NET y desarrollo web**, SDK .NET **10.0.402**, Git, PostgreSQL **18** y Vite+ **1.3.0**. Docker no es obligatorio.

En el equipo de preparación no había un SDK .NET visible ni Vite+ global. Se descargó el SDK portable oficial en `.tools/dotnet` y se verificó su SHA512 con los metadatos de Microsoft. Se usó Node **22.23.2**, ya existente bajo NVM, sin cambiar su selección global. Se descargaron los binarios portables de PostgreSQL **18.6** desde EDB. Estas herramientas y los datos privados están excluidos de Git y no se distribuyen al clonar.

En **cada terminal** de este equipo, desde la raíz, activa las herramientas locales:

```powershell
. ./infrastructure/scripts/Use-LocalTools.ps1
```

El script solo configura el proceso de PowerShell actual. No instala herramientas ni modifica el PATH del sistema.

## Vite+ global o local

Instalación global oficial opcional; este comando ejecuta un instalador remoto y debe ser ejecutado conscientemente por el usuario. **No se ejecutó durante esta preparación**:

```powershell
irm https://vite.plus/ps1 | iex
```

Abre una nueva terminal y comprueba:

```powershell
vp help
vp create --list
vp toolchain --global
```

El proyecto usa el CLI **local** fijado en package.json y pnpm-lock.yaml. El flujo oficial sin instalación global permite iniciar el generador mediante:

```powershell
pnpm dlx --package=vite-plus@1.3.0 vp help
```

Se consultaron `vp create --help` y `vp create --list` reales y se generó la base con:

```powershell
vp create vite:application --directory frontend/haqpos-web --package-manager pnpm --no-git --no-hooks --no-agent --no-editor --no-interactive -- --template react-ts
```

No repitas el generador sobre la aplicación existente. La plantilla generó React + TypeScript y migró sus herramientas a Vite+; se completó manualmente su paso final de formato tras resolver la ruta del CLI.

Sin CLI global, el script local agrega `frontend/haqpos-web/node_modules/.bin` al PATH para permitir los comandos `vp` directamente. Alternativamente, usa `pnpm exec vp`. El CLI local **no** admite `vp toolchain --global` ni `vp env pin`; el pin compatible se declara en `packageManager: pnpm@10.32.1`, conforme al mecanismo oficial de detección. No se ejecutaron instaladores remotos.

## HTTPS y Visual Studio

Una vez con `dotnet` disponible:

```powershell
./infrastructure/scripts/Initialize-DevelopmentHttps.ps1 -Trust
```

Windows puede solicitar confirmación para confiar en el certificado. Se exportan el certificado y la clave privada de desarrollo a `.tools/certs/localhost.pem` y `localhost.key`, fuera de Git. Durante la preparación se generó y exportó el certificado; **no se instaló confianza de navegador automáticamente**.

El perfil `https` de la API carga esos mismos archivos. Vite+ los usa para servir HTTPS y verificar el certificado del proxy sin desactivar la validación TLS. Sin exportación, el servidor frontend puede arrancar en HTTP; realiza el paso anterior para el flujo HTTPS completo.

Abre `HAQPOS.slnx` en Visual Studio 2026, establece **HAQPOS.Api** como proyecto de inicio y elige el perfil **https**. F5 abre `https://localhost:7143/api/health`. Para Visual Studio instala el SDK 10.0.402 mediante su instalador o el instalador oficial de Microsoft; la comprobación por CLI portable no sustituye esa integración del IDE. La apertura de Visual Studio no fue verificada en esta sesión.

## PostgreSQL 18

### Instancia portable ya configurada en este equipo

- Binarios: `.tools/postgresql/pgsql`.
- Datos: `.tools/postgresql/data`.
- Dirección: `127.0.0.1:55432`, base y usuario técnico `haqpos`.
- Contraseña aleatoria guardada únicamente en archivos locales ignorados y la conexión en User Secrets de la API.
- No se modificó el servicio PostgreSQL **16** existente en el puerto habitual.

```powershell
./infrastructure/scripts/Start-LocalPostgres.ps1
# Para detenerla:
./infrastructure/scripts/Start-LocalPostgres.ps1 -Stop
```

No es un servicio de Windows y puede ser necesario iniciarla después de reiniciar el equipo. Se comprobó PostgreSQL 18.6 mediante SQL, cero tablas públicas y conexión desde EF Core. La contraseña no se imprime en los scripts.

### Instalación nativa en otro equipo

Instala PostgreSQL 18 desde la [distribución oficial para Windows](https://www.postgresql.org/download/windows/). Elige un puerto libre, crea la base vacía `haqpos` y un usuario técnico, sin tablas de negocio. Configura la conexión usando una variable o User Secrets:

```powershell
$connectionString = Read-Host 'Cadena local de PostgreSQL (no se guarda en Git)'
dotnet user-secrets set 'ConnectionStrings:Haqpos' $connectionString --project backend/HAQPOS.Api
Remove-Variable connectionString
```

Formato: `Host=localhost;Port=55432;Database=haqpos;Username=haqpos;Password=<secreto-local>`. Nunca lo copies a appsettings versionados. También se acepta la variable `ConnectionStrings__Haqpos`. La API no carga archivos `.env` automáticamente.

### Docker opcional, solo base de datos

Con Docker Desktop iniciado en modo Linux:

```powershell
Copy-Item .env.example .env
# Edita .env y define POSTGRES_PASSWORD; nunca lo agregues a Git.
docker compose --env-file .env -f infrastructure/docker/compose.postgres.yaml up -d
```

Publica únicamente `127.0.0.1:55432`. Detén primero la instancia portable si usa ese puerto o cambia POSTGRES_PORT y la cadena de conexión. El volumen persiste los datos bajo el directorio adecuado para PostgreSQL 18.

## Ejecución

Terminal 1, raíz:

```powershell
. ./infrastructure/scripts/Use-LocalTools.ps1
./infrastructure/scripts/Start-LocalPostgres.ps1
dotnet run --project backend/HAQPOS.Api --launch-profile https
```

También puedes iniciar la API mediante F5.

Terminal 2, raíz:

```powershell
. ./infrastructure/scripts/Use-LocalTools.ps1
Set-Location frontend/haqpos-web
vp install --frozen-lockfile
vp dev
```

Abre `https://localhost:5173`. La pantalla muestra HAQPOS, el texto de configuración y el estado de la API.

| Servicio | URL predeterminada |
| --- | --- |
| Frontend | https://localhost:5173 |
| API | https://localhost:7143 |
| HTTP API, redirige a HTTPS | http://localhost:5143 |
| Salud | https://localhost:7143/api/health |
| PostgreSQL, diagnóstico Development | https://localhost:7143/api/health/database |
| OpenAPI Development | https://localhost:7143/openapi/v1.json |

Puertos API: `Properties/launchSettings.json`. Frontend y destino del proxy: copia `frontend/haqpos-web/.env.example` a `.env.local` y cambia FRONTEND_PORT / API_PROXY_TARGET. VITE_API_BASE_URL se mantiene en `/api`, mismo origen; VITE_* nunca contiene secretos. No se requiere CORS para este flujo.

## Comprobaciones

Detén la API antes de compilar para liberar sus DLL en Windows:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet tool restore
dotnet ef --version

Set-Location frontend/haqpos-web
vp install --frozen-lockfile
vp check
vp test run
vp build
```

`vp test` también está disponible; `run` asegura una sola ejecución en terminal interactiva. No se instalaron ESLint, Prettier, Vite o Vitest como herramientas independientes. TypeScript está en modo estricto tanto para la aplicación como para la configuración.

EF queda preparado para migraciones futuras mediante el manifiesto de dotnet-ef y el paquete Design en la API. No ejecutes creación de migraciones ni actualización de base en esta etapa.

## Incidencias del entorno de preparación

El sandbox de Windows bloqueaba TLS nativo de Vite+/.NET y el acceso al almacén de certificados. Para restaurar dependencias se utilizó temporalmente un intermediario HTTP en **loopback** que descargaba de los registros oficiales por HTTPS verificado con Python. No se guardó esa configuración en el repositorio: NuGet.Config conserva nuget.org y los manifiestos/lockfiles no contienen el intermediario. El diagnóstico local queda ignorado en `.tools`; no es una dependencia de HAQPOS ni una instrucción para desactivar TLS.

Se fijó xunit.runner.visualstudio **3.1.5**, con el que compilaron y pasaron las pruebas. El adaptador 4.0.1 probado inicialmente bloqueaba una copia de ensamblado en este entorno.

El runtime ASP.NET Core y PostgreSQL se verificaron fuera del sandbox cuando Windows necesitó acceso al certificado o crear tokens de proceso. No se realizaron cambios al PostgreSQL 16, instalaciones globales, commits ni pushes.
