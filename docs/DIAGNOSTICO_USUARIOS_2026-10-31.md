# Diagnóstico del flujo de usuarios de HAQPOS

Fecha: 31 de octubre de 2026. Revisión del código disponible en este equipo.

**Dictamen.** El sistema tiene una base de autenticación sólida para continuar el desarrollo: la autorización se comprueba en el servidor, las contraseñas se almacenan con hash y las sesiones pueden revocarse. Aun así, faltan controles para proteger las cuentas privilegiadas y hay fallos confirmados de mensajes, actualización de estados y recuperación. Recomiendo resolver las prioridades altas antes de utilizarlo en producción con información real.

La revisión comprende código de frontend y backend, persistencia, configuración de despliegue, pruebas existentes y reproducciones adicionales. Las pruebas de base de datos utilizaron esquemas aleatorios independientes; las cuentas actuales no se modificaron. Las reproducciones de interfaz usaron Edge con respuestas de API simuladas, sin ejecutar acciones sobre usuarios reales. No se evaluó un despliegue de producción ni su infraestructura real.

**Cómo funciona actualmente el proceso.**

| Etapa | Comportamiento observado | Evaluación |
| --- | --- | --- |
| Instalación | El administrador inicial se crea por consola y únicamente si todavía no existen usuarios. | Correcto: no hay registro público ni contraseña predeterminada. |
| Alta | Un administrador registra número de empleado, nombre y rol. El empleado es texto de 1 a 20 dígitos; conserva ceros iniciales. La base exige unicidad. | Correcto. El número no se puede editar después. |
| Contraseña temporal | El servidor genera 8 bytes aleatorios, representados como cuatro grupos hexadecimales. Caduca en 24 horas y se entrega en la respuesta de alta o restablecimiento. | El formato actual ofrece 64 bits aleatorios y puede conservarse. No se recupera desde los listados. |
| Almacenamiento | ASP.NET Identity V3, PBKDF2-HMAC-SHA512 con 220 000 iteraciones y sal. | Configuración coherente con la recomendación consultada de OWASP para esa variante de PBKDF2. |
| Login | Exige CSRF; verifica la contraseña, el estado activo y la vigencia de la temporal. Usa una comprobación de hash ficticia si la cuenta no existe. | Correcto en las pruebas realizadas. |
| Bloqueo | Supervisor y Cajero se bloquean 15 minutos tras cinco fallos. El bloqueo revoca sus sesiones. Los intentos durante un bloqueo no vuelven a extenderlo. | Correcto en API; hay problemas de actualización y mensajes en interfaz. |
| Administrador | No se bloquea por contraseñas incorrectas. | Decisión solicitada previamente; requiere protecciones compensatorias. |
| Primer ingreso | Sesión máxima de 10 minutos. Sólo permite consultar la sesión, salir y cambiar contraseña. Ni un administrador temporal puede administrar usuarios. | Correcto; comprobado por API. |
| Contraseña personal | Se exige la actual, confirmación, 15 a 128 caracteres, una nueva distinta y algunas comprobaciones contra claves previsibles. | El cambio invalida las sesiones anteriores y crea una nueva. Falta acceso ordinario a esta función en la interfaz. |
| Sesión normal | Cookie cifrada, Secure, HttpOnly, SameSite Strict y prefijo __Host-. Caduca por 20 minutos sin solicitudes y tiene un máximo absoluto de 8 horas. | La caducidad del servidor está implementada; la pantalla no se limpia al cumplirse el tiempo. |
| Roles | Administrador administra usuarios y auditoría. Administrador y Supervisor gestionan inventario y menú. Cajas está disponible para los tres roles. | Inventario y Menú tienen políticas en API. Cajas es una pantalla vacía; todavía no hay operaciones financieras que revisar. |
| Modificación de cuentas | Verificación administrativa, control de versiones y revocación de sesiones al cambiar rol o estado. No se permite desactivarse o retirarse el propio rol administrador. | Correcto como base. La versión también cambia por actividad de login y genera conflictos operativos. |
| Restablecimiento | Un administrador genera otra temporal para una cuenta activa ajena, desbloquea e invalida sus sesiones. | Correcto en seguridad básica; faltan protección frente a restablecimientos superpuestos y confirmación reciente de identidad. |
| Desbloqueo | Sólo administrador; cuenta ajena, activa y actualmente bloqueada. Reinicia contador y revoca sesiones. | Comprobado mediante prueba adicional. |
| Cierre de sesión | Revoca la sesión en PostgreSQL y elimina la cookie. Reutilizar la cookie cerrada no funciona. | Comprobado por integración. |
| Recuperación administrativa | Existe comando local para restablecer un administrador activo. | Hay una vía de recuperación cuando se pierde la contraseña del único administrador. Requiere un procedimiento de Sistemas y acceso controlado al servidor. |
| Auditoría | Registra altas, accesos, bloqueos, cambios, restablecimientos, desbloqueos y cierres. | Útil, pero insuficiente para investigar todos los incidentes. |

Evidencia principal: [UserService.cs](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:16), [UserRules.cs](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Application/Users/UserRules.cs:8), [PasswordSecurity.cs](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Security/PasswordSecurity.cs:9), [políticas y cookies](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Api/Program.cs:68). El factor de trabajo del hash coincide con [OWASP Password Storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).

**Hallazgos de prioridad alta.**

**U01. La API utiliza una cuenta PostgreSQL con privilegios de superusuario.**

Una consulta de sólo lectura al catálogo de PostgreSQL confirmó que la cuenta de la conexión local tiene `rolsuper = true`. Además, Compose conecta la API con el mismo `POSTGRES_USER` utilizado para inicializar PostgreSQL. En una instalación nueva con la imagen oficial, esa cuenta es superusuario. Esto amplifica el impacto de una futura vulnerabilidad de la aplicación: podría permitir alterar roles de base, eliminar información o modificar la auditoría. No significa que se haya demostrado una inyección SQL.

Recomendación: separar la cuenta de migraciones de la cuenta de ejecución; quitar privilegios de superusuario, creación de roles y DDL a la API; conceder únicamente los permisos necesarios. La auditoría debería tener permisos que impidan su actualización y eliminación desde la aplicación ordinaria.

Evidencia: [compose.yaml](C:/Users/QRSIS05/Desktop/HAQPOS/infrastructure/docker/compose.yaml:27) y [documentación de la imagen oficial PostgreSQL](https://hub.docker.com/_/postgres).

**U02. El administrador está exento de bloqueo y no tiene MFA ni límite de intentos por cuenta independiente de la IP.**

Se confirmó que cinco errores no bloquean al administrador ni cierran su sesión. Esto cumple lo solicitado. La protección restante es de 15 solicitudes por minuto por IP, en memoria y por instancia. No impide repartir intentos contra la misma cuenta entre varias IP. Además, un equipo o una salida de red compartida puede consumir el cupo de otros empleados; el mismo cupo cubre login, cambio de contraseña y desbloqueo.

Recomendación: conservar la exención de bloqueo del administrador y añadir MFA, limitación progresiva por cuenta y por origen, alertas y controles compartidos si se despliega más de una instancia. Verificar la IP efectiva detrás del proxy en producción. El código no implementa un segundo factor.

Evidencia: [exención administrativa](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:30), [límite por IP](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Api/Program.cs:99) y [OWASP MFA](https://cheatsheetseries.owasp.org/cheatsheets/Multifactor_Authentication_Cheat_Sheet.html).

**U03. Las acciones administrativas sensibles no exigen confirmar de nuevo la identidad.**

Una sesión administrativa válida puede crear otro administrador, cambiar roles, restablecer contraseñas y desbloquear cuentas. Hay confirmaciones visuales para algunas acciones, pero no comprobación reciente de contraseña o segundo factor. Si una sesión se utiliza desde un puesto desatendido o es comprometida, permite apropiarse de otras cuentas sin conocer la contraseña actual del administrador.

Recomendación: exigir autenticación reciente para elevar permisos, restablecer contraseñas y otras acciones de alto impacto. La confirmación debe validarse en el servidor y tener vigencia breve.

Evidencia: [endpoints administrativos](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Api/Users/UserEndpoints.cs:55), [validación administrativa](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:350) y [OWASP Authentication, funciones sensibles](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#require-re-authentication-for-sensitive-features).

**Fallos y carencias de prioridad media.**

**U04. La sesión puede caducar en el servidor y la pantalla conservar información visible.**

El navegador sólo revalida la sesión al cargar, recuperar el foco o realizar determinadas solicitudes. No existe limpieza automática de la pantalla por inactividad. En una reproducción con reloj controlado, la tabla de usuarios permaneció visible después de 21 minutos sin reconsultar la sesión. El diálogo de contraseña temporal tampoco tiene cierre por tiempo. Esto expone información en puestos compartidos, aunque las siguientes solicitudes protegidas sí se rechacen en el servidor.

Recomendación: advertencia y limpieza local al vencer el tiempo; conservar la validación del servidor como autoridad; ocultar y cerrar el diálogo de credenciales tras un plazo breve. Sincronizar el cierre de sesión entre pestañas para reducir estados desactualizados. OWASP recomienda complementar los plazos del servidor con [caducidad visible en el cliente](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html#automatic-client-logout).

Evidencia: [AuthProvider](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/app/auth.tsx:8), [diálogo temporal](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/users-page.tsx:175) y reproducción de navegador.

**U05. El botón de desbloqueo puede faltar porque la tabla quedó desactualizada.**

Si un empleado se bloquea después de que el administrador abrió la lista, la consulta conserva `lockedUntil = null`. El temporizador de la página sólo actualiza la hora local; no solicita el nuevo estado. No hay botón explícito de actualizar. Se reprodujo: no apareció el icono después del bloqueo simulado; apareció al cambiar la búsqueda y forzar otra consulta.

Recomendación: actualización manual visible y actualización periódica moderada mientras la página está visible, o notificación de cambios. Volver a obtener los datos después de un conflicto. No es un problema de autorización del endpoint de desbloqueo.

Evidencia: [consulta de usuarios](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/users-page.tsx:343), [condición del icono](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/users-page.tsx:501) y [configuración de consultas](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/app/query-client.ts:3).

**U06. En el primer cambio de contraseña puede perderse el aviso de bloqueo.**

El quinto error en la contraseña actual bloquea a Supervisor o Cajero y revoca las sesiones, pero la respuesta sigue siendo HTTP 401 con el texto de contraseña incorrecta. El frontend guarda ese error y después refresca la sesión; al recibir usuario nulo monta el login y pierde el mensaje. Se confirmó tanto la respuesta real del backend como la transición de interfaz. En el siguiente intento de login sí aparece el mensaje HTTP 423 con Sistemas y los 15 minutos.

Recomendación: responder con un código de bloqueo estable y conservar un aviso de causa al volver al login. Tratar también la caducidad de la sesión temporal durante el llenado del formulario.

Evidencia: [ChangePasswordAsync](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:113) y [manejo de error del formulario](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/password-page.tsx:42).

**U07. Los usuarios normales no tienen acceso en la interfaz para cambiar su contraseña personal.**

La API y el componente existen, pero la única ruta de cambio se monta cuando `mustChangePassword` es verdadero. Una persona que sospecha que alguien conoce su contraseña debe pedir un restablecimiento; el administrador tampoco puede restablecer su propia contraseña desde Control de usuarios. Esto procede de la retirada solicitada del módulo independiente de cambio de contraseña.

Recomendación: ofrecer una acción discreta en el perfil o menú de la cuenta, reutilizando la función existente y solicitando la contraseña actual. Puede resolverse sin volver a añadir un módulo lateral.

Evidencia: [rutas](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/app/app.tsx:49), [componente de cambio](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/password-page.tsx:8) y [restricción de restablecimiento propio](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:282).

**U08. Los intentos de login provocan conflictos al editar usuarios.**

El mismo campo `Version` protege nombre, rol y estado, pero también cambia al iniciar sesión o fallar una contraseña. Se confirmó que abrir un usuario, producir un solo error de login y guardar un cambio de nombre devuelve HTTP 409. El formulario conserva la versión antigua y no refresca la lista automáticamente al recibir el conflicto; repetir Guardar puede volver a fallar.

Recomendación: separar la concurrencia de los datos administrables de la actividad de autenticación, o actualizar y reconciliar el formulario al recibir 409 conservando los datos escritos. Mantener la protección contra sobrescrituras reales.

Evidencia: [versiones durante login](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:54), [validación de edición](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:218) y [formulario](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/users-page.tsx:60).

**U09. Dos restablecimientos pueden entregar contraseñas distintas con aparente éxito.**

Restablecer no exige la versión del usuario ni un identificador de operación. Se comprobaron dos restablecimientos consecutivos: ambos respondieron HTTP 200, pero la primera temporal quedó inválida. Dos administradores pueden entregar a una persona una credencial que ya no sirve. También existe el caso de una respuesta perdida después del commit: la contraseña anterior ya cambió, aunque el operador no haya recibido la nueva. Este último caso se deduce del orden de commit y respuesta; no se simuló un corte real de red durante el commit.

Recomendación: control de versión para el restablecimiento, manejo de solicitudes duplicadas y mensajes que aclaren cuándo una operación pudo haberse aplicado. Definir un proceso seguro para regenerar la temporal si se pierde la respuesta.

Evidencia: [contratos de usuario](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Application/Users/UserContracts.cs:9), [endpoint de restablecimiento](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Api/Users/UserEndpoints.cs:65) y [ResetPasswordAsync](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:282).

**U10. La auditoría no permite reconstruir todos los incidentes.**

Se guarda la IP, pero el DTO y la pantalla no la muestran. `user.updated` no conserva qué rol, nombre o estado cambió ni los valores anterior y posterior. Los intentos sobre empleados inexistentes quedan con cuenta relacionada nula. No hay eventos específicos en la auditoría de negocio para rechazos por permisos, CSRF, límite de intentos o vencimiento de sesión. Algunos de esos hechos pueden existir en los registros técnicos HTTP; no equivalen a una auditoría consultable con contexto. Tampoco hay filtros por empleado, acción o intervalo de fechas ni alertas automáticas.

Recomendación: registrar el resultado y motivo seguro, contexto de cuenta y cambios de permisos/estado; exponer IP y filtros sólo a personal autorizado; añadir alertas de accesos sospechosos. Evitar registrar contraseñas, cookies, tokens o identificadores de sesión utilizables. Proteger la auditoría contra modificación.

Evidencia: [entidad de auditoría](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Domain/Users/UserAccount.cs:34), [consulta](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:299) y [pantalla](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/audit-page.tsx:75). Criterio: [OWASP Logging](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html).

**U11. Faltan herramientas para administrar sesiones y su conservación.**

Se permiten varias sesiones de la misma cuenta en distintos clientes. El usuario no puede verlas ni cerrar una sesión ajena de forma selectiva. `UserSession` no guarda dispositivo o IP de origen. No hay una tarea implementada que elimine sesiones vencidas o revocadas ni una política de archivo de auditoría; los registros crecen con el uso. En desarrollo, reiniciar el API invalida las cookies por las claves efímeras, pero no limpia las filas antiguas.

Recomendación: definir si un cajero puede operar en varios puestos, mostrar sesiones y permitir revocación; establecer retención y limpieza por lotes e índices adecuados. La política debe distinguir datos transitorios de sesiones y la auditoría que Sistemas necesite conservar.

Evidencia: [UserSession](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Domain/Users/UserAccount.cs:24), [creación de sesión](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:335) y [configuración de persistencia](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Persistence/HaqposDbContext.cs:33).

**U12. El manejo de errores y solicitudes necesita ser más consistente.**

Se confirmó que un corte de red en login muestra `Failed to fetch`. No existe un tiempo máximo de aplicación para las peticiones de escritura ni un protocolo de cancelación o conciliación del resultado. El cliente sólo extrae `title` de Problem Details; no conserva códigos funcionales, `traceId` ni `Retry-After` para orientar al usuario. Una respuesta 403 sin título útil acaba como un texto técnico HTTP.

Una prueba adicional confirmó que un JSON de login con tipo de dato incorrecto termina en HTTP 500 en el entorno Development: el manejador genérico trata el error de petición como error interno. Este resultado se verificó en Development, no en un despliegue de producción.

Recomendación: mensajes en español por clase de error; códigos estables para bloqueo, caducidad, CSRF y permisos; referencia de soporte para errores internos; tiempo máximo de espera y una forma segura de tratar operaciones cuyo resultado sea desconocido. Clasificar los errores de petición como 400 y una indisponibilidad de servicio de forma distinguible. Evitar reintentos automáticos de altas o restablecimientos sin protección contra duplicados.

Evidencia: [cliente HTTP](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/services/http-client.ts:12), [errorMessage](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/services/users.ts:72) y [manejador global](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Api/Errors/GlobalExceptionHandler.cs:43).

**U13. Promover una cuenta bloqueada a Administrador deja un estado de bloqueo incoherente.**

El cambio de rol conserva `LockedUntil` y el contador de errores. La API ignora el bloqueo para Administrador, pero la tabla evalúa la fecha sin considerar esa exención. Se confirmó que el cambio a Administrador devuelve todavía una fecha futura de bloqueo, aunque esa cuenta puede iniciar sesión correctamente de inmediato. La tabla puede mostrar Bloqueado y ofrecer Desbloquear hasta que el nuevo administrador ingrese o se actualice su estado.

Recomendación: normalizar los contadores y el bloqueo cuando cambie la política aplicable al rol; calcular un estado de acceso consistente entre servidor e interfaz.

Evidencia: [cambio de rol](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Infrastructure/Users/UserService.cs:225) y [UserStatus](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/users-page.tsx:26).

**Mejoras de prioridad baja y mantenimiento.**

**U14. El filtro de contraseñas previsibles es pequeño y sus reglas no se muestran completas.**

La lista sólo contiene siete patrones y no compara contra un catálogo amplio de contraseñas comprometidas. Además, la interfaz explica la longitud y la coincidencia, pero no anticipa todas las restricciones del backend. Un usuario puede cumplir las dos marcas visibles y aun recibir un rechazo por previsibilidad. El hash y la longitud están bien como base; mejorar la comprobación de claves conocidas sería un refuerzo.

Recomendación: usar un catálogo apropiado, preferentemente con una opción local o respetuosa de la privacidad, y dar indicaciones coherentes con el servidor sin transmitir la contraseña en claro a un servicio externo. Evidencia: [UserRules.Password](C:/Users/QRSIS05/Desktop/HAQPOS/backend/HAQPOS.Application/Users/UserRules.cs:36) y [comprobaciones visibles](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/pages/password-page.tsx:116).

**U15. Hay deuda de pruebas y presentación que impide considerar la revisión totalmente limpia.**

Una prueba unitaria exige exactamente tres entidades y actualmente existen seis. El fallo corresponde a una expectativa desactualizada al añadirse Inventario y Menú; no demuestra una vulnerabilidad de autenticación. Las pruebas frontend existentes sólo cubren dos comportamientos básicos del cliente HTTP. Faltan regresiones permanentes de varios casos descritos aquí. También hay textos con codificación incorrecta en `app.tsx`, y el comprobador de formato reporta dos archivos.

Recomendación: sustituir el conteo rígido por comprobaciones del modelo necesarias para seguridad; conservar pruebas de los fallos confirmados, ampliar cobertura de interfaz y corregir codificación/formato. Evidencia: [prueba de persistencia](C:/Users/QRSIS05/Desktop/HAQPOS/tests/HAQPOS.UnitTests/PersistenceConfigurationTests.cs:27), [pruebas frontend](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/services/http-client.test.ts:6) y [textos de sesión](C:/Users/QRSIS05/Desktop/HAQPOS/frontend/haqpos-web/src/app/app.tsx:22).

**Mensajes de error: comportamiento y diagnóstico.**

| Situación | Respuesta actual | Diagnóstico |
| --- | --- | --- |
| Contraseña incorrecta, intentos 1 a 4 | HTTP 401, mensaje genérico. | Adecuado para evitar distinguir credenciales equivocadas. |
| Quinto fallo de login de Supervisor o Cajero | HTTP 423, bloqueo temporal, Sistemas y 15 minutos. | Correcto; confirmado. |
| Nueva petición de login de una cuenta bloqueada | HTTP 423, mismo aviso. | Correcto. Podría informar la hora de desbloqueo o el tiempo restante. |
| Empleado inexistente, cuenta inactiva o temporal vencida | HTTP 401, mensaje genérico. | Evita revelar estados distintos; conviene añadir orientación general de contacto con Sistemas y recuperación del primer acceso. |
| Contraseña actual incorrecta al cambiarla | HTTP 401, contraseña actual incorrecta. | En el quinto fallo no explica el bloqueo y el frontend pierde el mensaje al salir. |
| Sesión vencida o revocada | HTTP 401 en recursos protegidos; `/auth/me` responde usuario nulo. | Falta un aviso persistente que explique por qué se volvió al login. |
| CSRF inválido o caducado | HTTP 400, pide actualizar. | Mensaje claro; el cliente debe gestionar cambios de sesión sin perder el contexto de la operación. |
| Límite de credenciales | HTTP 429 y `Retry-After: 60`. | El texto existe, pero no se utiliza el encabezado para la espera visible. El límite se comparte por IP y por varias acciones. |
| Permiso insuficiente | HTTP 403. | Falta una presentación uniforme y accionable en español. |
| Empleado duplicado | HTTP 409, mensaje específico. | Correcto para un formulario administrativo. |
| Versión de usuario desactualizada | HTTP 409, solicita actualizar la lista. | La interfaz no resuelve automáticamente el estado antiguo. |
| Fallo interno o de base de datos | HTTP 500 con título genérico y referencia técnica en el caso genérico. | No expone la traza al usuario, pero falta guiar al operador y mostrar una referencia de soporte. |
| Fallo de red | Error original de `fetch`. | Reproducción: mensaje en inglés sin orientación. |
| JSON con tipo incorrecto en Development | HTTP 500. | Clasificación errónea de un problema de petición; confirmado en el entorno indicado. |

El aviso explícito de bloqueo permite inferir que un número de empleado corresponde a una cuenta bloqueada. Es una consecuencia de la experiencia solicitada, no un acceso indebido. Debe compensarse con controles contra enumeración y abuso. Asimismo, el bloqueo automático puede ser provocado por un tercero que conozca el empleado: cinco fallos expulsan sesiones válidas. Conviene monitorear ese patrón y conservar la política de disponibilidad que decida la institución.

**Pruebas y evidencia de esta revisión.**

| Comprobación | Resultado |
| --- | --- |
| Pruebas unitarias existentes de .NET | 13 correctas y 1 fallida de 14. Fallo: conteo de entidades esperado 3, actual 6. |
| Pruebas de integración existentes | 15 correctas, 0 omitidas. Incluyen 12 casos de usuarios contra PostgreSQL y 3 de salud/configuración. |
| Pruebas frontend existentes | 2 correctas. |
| `vp check` | Reporta formato en `src/pages/login-page.tsx` y `src/styles/globals.css`. No se aplicó corrección automática durante el diagnóstico. |
| Comprobaciones adicionales de API/servicio | 8 correctas como pruebas de diagnóstico de comportamientos concretos. Algunas afirman explícitamente la presencia del fallo, por ejemplo el HTTP 500 de Development; no significan que esos comportamientos sean deseables. |
| Reproducciones de navegador | 4 confirmadas con respuestas simuladas de API y datos ficticios. |
| Privilegios de la conexión local | `rolsuper = true`, consultado sin leer hashes ni contraseñas de usuarios. |
| Limpieza de esquemas de prueba | 0 esquemas `auth_test_<guid>` restantes al finalizar. |
| Archivo de credenciales iniciales mencionado en el editor | `.tools/bootstrap/administrador-inicial.txt` no existe en disco durante esta revisión. La pestaña del editor no prueba que siga existiendo un archivo actual. |
| Auditoría de vulnerabilidades de paquetes | No concluyente: NuGet reportó NU1900 por falta de acceso al índice. No se afirma que las dependencias estén libres de vulnerabilidades. |

Las ocho comprobaciones adicionales verificaron: exención del administrador; autorización y resultado del desbloqueo; promoción de usuario bloqueado; dos restablecimientos consecutivos; quinto fallo del cambio de contraseña; conflicto de edición provocado por un login fallido; vencimiento absoluto de 8 horas con actividad regular; y clasificación del JSON incorrecto en Development.

La evidencia reproducible se dejó en [`.artifacts/user-security-review`](C:/Users/QRSIS05/Desktop/HAQPOS/.artifacts/user-security-review): resultados TRX de la suite, ocho comprobaciones adicionales y `browser-results.json`. Los archivos están en una ubicación ignorada por Git. El arnés adicional se incluyó sólo mediante una opción de compilación de diagnóstico, sin editar el proyecto de pruebas ni la aplicación.

**Orden recomendado de trabajo.**

1. Antes de producción: separar privilegios de base, MFA para administradores, autenticación reciente para operaciones sensibles y protección contra intentos distribuidos. Conservar la exención de bloqueo del administrador solicitada.
2. Corregir los fallos visibles: expiración de pantalla, mensajes al bloquearse o caducar, actualización de la lista y gestión de conflictos, transición de roles y restablecimientos superpuestos.
3. Habilitar un cambio de contraseña personal desde la cuenta y una recuperación operativa clara; conservar la verificación de contraseña actual y la revocación de sesiones.
4. Completar auditoría, alertas, consulta/revocación de sesiones y políticas de retención y limpieza.
5. Convertir las reproducciones en regresiones permanentes y comprobar el despliegue real: HTTPS y cabeceras, IP detrás del proxy, permisos de claves y base, rotación y recuperación de respaldos. Repetir la auditoría de dependencias cuando esté disponible el servicio.

Esta revisión dejó un diagnóstico y artefactos de comprobación. No aplicó correcciones al comportamiento de la aplicación.
