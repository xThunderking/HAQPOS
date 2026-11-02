# Usuarios y seguridad

## Alcance

El número de empleado es el identificador de acceso y se conserva como texto: `00123` y `123` son empleados distintos. Se admiten de 1 a 20 dígitos ASCII, sin letras. El nombre completo admite de 3 a 150 caracteres. No se elimina físicamente una cuenta: se desactiva para conservar la trazabilidad.

| Función | Administrador | Supervisor | Cajero |
| --- | --- | --- | --- |
| Ingresar y cambiar su contraseña | Sí | Sí | Sí |
| Registrar, editar y desactivar otras cuentas | Sí | No | No |
| Asignar cualquiera de los tres roles | Sí | No | No |
| Generar una nueva contraseña temporal para otra persona | Sí | No | No |
| Consultar usuarios y actividad de seguridad | Sí | No | No |

Los permisos de supervisión y operación de caja se incorporarán junto con esos módulos. Ninguno obtiene acceso administrativo por su nombre de rol. La API es la autoridad de permisos, independientemente de la interfaz.

## Registro y primer ingreso

1. Un administrador captura número de empleado, nombre completo y rol.
2. El servidor genera una contraseña aleatoria con el formato `A1B2-C3D4-E5F6-7890`: cuatro grupos de cuatro caracteres hexadecimales en mayúsculas, fáciles de leer y dictar. La respuesta del alta la entrega una sola vez; no puede recuperarse desde un listado ni consultarse después. En PostgreSQL únicamente se guarda su hash con sal.
3. La persona recibe la contraseña por un canal seguro e ingresa con su número de empleado.
4. La contraseña temporal caduca a las 24 horas. La sesión inicial dura como máximo 10 minutos y únicamente permite consultar la propia sesión, cerrar sesión y cambiar la contraseña. No concede acceso al control de usuarios, aunque el rol asignado sea Administrador.
5. El usuario confirma su contraseña actual y elige una nueva, de 15 a 128 caracteres. Puede usar una frase con espacios y Unicode. Se rechazan contraseñas comunes, repetitivas, iguales a la actual o que incluyan su número de empleado (de tres o más dígitos). No se consulta una base externa de contraseñas filtradas.
6. Se elimina la condición temporal, se invalidan las sesiones anteriores y se abre una nueva sesión normal. Un restablecimiento posterior repite el proceso.

La contraseña temporal se mantiene solo en el estado del diálogo hasta cerrarlo. No se guarda en localStorage, sessionStorage, caché de consultas ni registros de aplicación. El comando local de instalación la entrega directamente a la consola del operador; no redirigir esa salida a registros compartidos.

## Protecciones implementadas

- Hash mediante `PasswordHasher<T>` de ASP.NET Core Identity V3, PBKDF2-HMAC-SHA512 con 220 000 iteraciones y sal aleatoria por contraseña. Se admite actualización de hashes al iniciar sesión.
- Cookie cifrada `__Host-haqpos.session`, `Secure`, `HttpOnly`, `SameSite=Strict`, ruta `/`, sin dominio y sin persistencia tras cerrar el navegador. No se usan JWT almacenados en JavaScript.
- Sesiones registradas en PostgreSQL: 20 minutos de inactividad y un máximo absoluto de 8 horas. La inactividad se actualiza como máximo una vez por minuto, por lo que el corte puede adelantarse hasta un minuto. La interfaz no mantiene una sesión viva con consultas periódicas.
- Validación del usuario, sesión y sello de seguridad en cada solicitud autenticada. Desactivar, cambiar rol, restablecer o cambiar contraseña invalida las sesiones previas. Cerrar sesión revoca la sesión también en el servidor para impedir reutilizar su cookie.
- CSRF obligatorio en todas las operaciones de escritura, incluido el inicio de sesión. Token ligado a la identidad actual, solicitado antes de cada envío. No se habilita CORS.
- Bloqueo de cuentas Supervisor y Cajero durante 15 minutos después de cinco fallos. Las cuentas Administrador no se bloquean por intentos fallidos; los intentos siguen en auditoría y el límite de 15 solicitudes por minuto por IP continúa aplicándose. Los contadores de las demás cuentas se almacenan en PostgreSQL y las comprobaciones bloquean la fila para no perder intentos simultáneos. Un administrador activo puede desbloquear antes desde Control de usuarios; el servidor valida el rol, impide el auto-desbloqueo, revoca sesiones, reinicia el contador y registra actor, cuenta e IP en auditoría. Restablecer una contraseña también desbloquea una cuenta.
- Máximo de 15 solicitudes de credenciales por minuto y dirección IP, con HTTP 429. Este límite en memoria es por instancia; antes de escalar a varias réplicas debe complementarse con un límite compartido en el proxy.
- Mensaje genérico para datos incorrectos, cuentas inactivas y temporales caducadas; cuando una cuenta activa está bloqueada, el inicio de sesión informa que contacte a Sistemas. Verificación de hash ficticio para cuentas inexistentes.
- Validación de entradas, parámetros SQL, unicidad de empleado en la base y versión de concurrencia para evitar sobrescribir ediciones desactualizadas.
- Prohibición de desactivar o degradar la propia cuenta administradora. Cambios administrativos serializados mediante bloqueo transaccional PostgreSQL para preservar un administrador activo.
- Auditoría de altas, modificaciones, recuperación administrativa, acceso, cambio y restablecimiento de contraseñas y cierre de sesión. Registra actor, cuenta, hora UTC e IP; no contraseñas, cookies ni tokens. El listado muestra los números de empleado y la hora local del navegador.
- Respuestas API sin caché, errores sin trazas internas y límite de cuerpo de 16 KiB. Nginx añade CSP, bloqueo de marcos, HSTS y permisos restringidos al desplegar.

## Instalación y recuperación local

No existe registro público, endpoint de instalación abierto ni contraseña predeterminada. Desde una terminal autorizada, con la conexión de PostgreSQL disponible:

```powershell
. ./infrastructure/scripts/Use-LocalTools.ps1
dotnet run --project backend/HAQPOS.Api --launch-profile https -- --migrate
dotnet run --project backend/HAQPOS.Api --launch-profile https -- --create-admin --employee 50052143 --name "SILVESTRE IGNACIO REYNA RAYO"
```

El alta inicial solo se permite cuando no existe ningún usuario. La consola muestra la contraseña temporal una sola vez. El número y nombre anteriores son los solicitados para este equipo; no se crean automáticamente en otros entornos.

Si el único administrador pierde su acceso o deja caducar la contraseña temporal, un operador autorizado del servidor puede generar otra:

```powershell
dotnet run --project backend/HAQPOS.Api --launch-profile https -- --reset-admin-password --employee 50052143
```

La recuperación solo acepta administradores activos, revoca sus sesiones y registra un evento. El acceso al servidor, a sus secretos de conexión y a esta herramienta debe limitarse a personal autorizado. Los usuarios normales solicitan el restablecimiento al administrador.

## HTTPS y claves de autenticación

En desarrollo, la API y Vite usan el mismo certificado PEM. En Windows, Schannel necesita cargar la clave en un contenedor del usuario: si el sandbox bloquea ese acceso, ejecutar la API desde una terminal normal autorizada. No se modifica la confianza de certificados al arrancar. El navegador debe confiar en el certificado de desarrollo. El proxy conserva la validación del certificado de la API; no se desactiva TLS.

En Development y Testing las claves de Data Protection son efímeras y se cierran las sesiones al reiniciar el proceso. En producción el arranque exige:

- `DataProtection__KeysPath`: directorio persistente de claves cifradas, escribible por el proceso de la API.
- `DataProtection__CertificatePath` y `DataProtection__PrivateKeyPath`: certificado RSA y clave PEM dedicada que protege esas claves. Restringir el acceso de lectura de la clave privada.

Compose incluye el volumen `data_protection_keys` y montajes de solo lectura desde `DATA_PROTECTION_CERT_PATH` y `DATA_PROTECTION_KEY_PATH`. El usuario no privilegiado de la imagen (`APP_UID`) debe poder escribir el volumen y leer únicamente los certificados necesarios. TI debe preparar permisos, certificados, respaldos y rotación: conservar los certificados anteriores para descifrar claves antiguas o planear un cierre global de sesiones durante la rotación. No se ha desplegado producción.

Aplicar las migraciones explícitamente antes de abrir la aplicación. En Docker, usar `docker compose ... run --rm api --migrate` y el mismo comando con `--create-admin` para la instalación. La cuenta de ejecución de base debe tener los permisos necesarios para las operaciones de usuarios, sesiones y auditoría; el operador de migraciones necesita además permisos de DDL. No exponer PostgreSQL ni la API directamente a internet.

La autenticación implementada usa contraseña, sin segundo factor. Los módulos futuros deben exigir la política `ActivePassword` y sus roles en el servidor. Definir con la institución la retención de auditoría, purga de sesiones vencidas, respaldos y requisitos de segundo factor antes de un uso hospitalario real.

## Validación

`dotnet test` cubre reglas y hashes. Para ejecutar además los casos contra PostgreSQL, proporciona `HAQPOS_TEST_CONNECTION` a una conexión autorizada a crear esquemas. Cada prueba crea `auth_test_<guid>`, aplica las migraciones allí y elimina ese esquema al finalizar. No usa las tablas públicas ni crea usuarios de demostración en ellas. Sin la variable, los casos PostgreSQL aparecen como omitidos explícitamente.

Se prueban cambio obligatorio, roles por API, cookies y CSRF, unicidad, bloqueo, caducidad temporal, sesiones, cierre y reutilización de cookies, desactivación, versiones de edición, límite de intentos y cierre del alta inicial. Frontend: `vp check`, `vp test run`, `vp build`.

Referencias: [autenticación por cookie de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0), [protección CSRF](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0), [almacenamiento de contraseñas de OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
