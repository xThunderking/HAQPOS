# Preparación de despliegue en Ubuntu Server LTS

Estos archivos son una base para el servidor **interno** del hospital. No se realizó despliegue, publicación en internet ni ejecución de imágenes Windows.

## Archivos

- `infrastructure/docker/api.Dockerfile`: SDK .NET 10.0.402, restauración con lockfiles, publicación Release y runtime ASP.NET Core 10.0.13 como usuario no root.
- `infrastructure/docker/frontend.Dockerfile`: imagen oficial Vite+ 1.3.0, `vp install --frozen-lockfile`, check, pruebas y build; salida estática en Nginx 1.30.2.
- `infrastructure/docker/compose.yaml`: API, Nginx, PostgreSQL 18.6, redes internas, dependencias por health check y volumen.
- `infrastructure/docker/compose.postgres.yaml`: alternativa de desarrollo que solo publica PostgreSQL en loopback.
- `infrastructure/nginx/haqpos.conf`: TLS, proxy API, encabezados y fallback SPA.
- `.dockerignore`: excluye herramientas locales, secretos, claves, dependencias y resultados de compilación.
- `.env.example`: parámetros públicos de referencia y campo vacío para contraseña.

Los dos Dockerfiles usan contexto de compilación en la raíz; comparten el .dockerignore raíz. Los paquetes frontend se instalan dentro de la imagen oficial de Vite+, siguiendo el [procedimiento oficial para SPA](https://viteplus.dev/guide/docker). No hay servidor `vp dev` en producción.

## Requisitos previos de TI

Ubuntu Server LTS aprobado por TI, Docker Engine con Compose, DNS interno, acceso de salida para construir/descargar imágenes y certificados de la CA interna. No se cambia la red del hospital desde estos archivos.

Copia el repositorio al servidor. Fuera del repositorio, prepara la clave privada y cadena de certificados, por ejemplo:

```text
/etc/haqpos/tls/fullchain.pem
/etc/haqpos/tls/privkey.pem
```

Restringe los permisos de esos archivos y del .env. Distribuye confianza de la CA a los equipos clientes mediante el procedimiento de TI. No reutilices certificados de desarrollo.

## Configuración y arranque futuro

Desde la raíz:

```bash
cp .env.example .env
chmod 600 .env
# Editar .env: contraseña, APP_HOST, certificados y parámetros internos.
docker compose --env-file .env -f infrastructure/docker/compose.yaml config --quiet
docker compose --env-file .env -f infrastructure/docker/compose.yaml build
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
docker compose --env-file .env -f infrastructure/docker/compose.yaml ps
```

El bind predeterminado es **127.0.0.1:8443**: no publica la aplicación en la LAN automáticamente. Para acceso hospitalario, TI debe asignar una IP privada concreta en BIND_ADDRESS, definir HTTPS_PORT, ajustar APP_HOST y permitir exclusivamente los clientes internos en el firewall. No usar una IP pública, redirección de puertos de internet ni reglas de apertura global.

El API y PostgreSQL no publican puertos en producción. Nginx conecta al API por una red interna; el API conecta a PostgreSQL por otra. Nginx utiliza la IP fija `172.30.43.10`, la única que el API confía para encabezados reenviados. Si esa subred colisiona con la LAN o VPN, TI debe cambiar **la subred y las dos referencias a la IP** en Compose. El límite de confianza permanece en un salto.

El API responde HTTP solo dentro de Docker; Nginx termina TLS y reemplaza los encabezados del cliente antes de reenviarlos. La API conserva AllowedHosts y HSTS. CORS no se abre porque todo el acceso del navegador utiliza el mismo origen HTTPS.

## Persistencia y salud

- Volumen `postgres_data` montado en `/var/lib/postgresql`, conforme al esquema versionado de PostgreSQL 18.
- `pg_isready` verifica la disponibilidad del servidor PostgreSQL.
- La API usa `dotnet HAQPOS.Api.dll --health-probe` para consultar su endpoint local, sin instalar curl en el runtime.
- Nginx tiene `/healthz` únicamente en el puerto HTTP no publicado; Compose espera que la API esté saludable.
- El health check de la API es liveness y no prueba credenciales de PostgreSQL. El diagnóstico independiente de la conexión se comprobó en Development; no se expone en producción.

No hay creación automática de esquema ni migraciones en el arranque. No ejecutes `docker compose down -v` salvo que quieras eliminar expresamente los datos persistidos. Antes de usar datos reales, TI debe definir respaldos, restauración, rotación de secretos y permisos de base adecuados.

## Verificado y pendiente

`docker compose config --quiet` pasó en ambos archivos con Compose **5.4.0** y valores de validación no sensibles. Se comprobó la existencia de los tags de Nginx 1.30.2-alpine y PostgreSQL 18.6-bookworm en el registro oficial. No se descargaron ni construyeron las imágenes porque Docker Desktop no tenía el daemon iniciado.

Quedan por comprobar con Docker activo: builds completos, sintaxis Nginx en su imagen, montaje de certificados internos y salud de todos los contenedores. No se verificó ejecución en Ubuntu, acceso LAN, firewall ni certificados de TI.
