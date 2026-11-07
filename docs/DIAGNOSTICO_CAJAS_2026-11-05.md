# Diagnóstico del módulo de Cajas

Fecha: 5 de noviembre de 2026. Revisión de la implementación del espacio de trabajo actual.

Se revisaron API, permisos, servicios, transacciones y bloqueos de PostgreSQL, restricciones de tablas, interfaz, reintentos, inventario, clientes, pagos, CXC, consultas y generación del corte. Se prepararon comprobaciones en una base temporal independiente y con compilación separada de la API de uso local. Las pruebas diagnósticas están en `.tools/cash-review`; sus resultados indican qué comportamientos se reprodujeron, incluso cuando el comportamiento reproducido es indeseable.

La base de aislamiento por usuario/área y los bloqueos de existencias están implementados. Los principales pendientes son la autorización de cortesías, la trazabilidad financiera y el control del dinero físico. La revisión no modifica la lógica de ventas.

## Hallazgos por prioridad

| ID | Prioridad | Naturaleza | Hallazgo |
|---|---|---|---|
| CAJ-01 | Alta | Permisos / fraude | El cajero puede cambiar un cliente a CORTESIA y cerrar su propio consumo en cero. |
| CAJ-02 | Alta | Trazabilidad | Los cambios de clientes no identifican al cliente afectado; el visor pierde la referencia a cuentas de caja. |
| CAJ-03 | Alta | Control financiero | El cierre no registra efectivo contado, diferencia ni entrega: todavía falta el arqueo. |
| CAJ-04 | Alta operativa | Recuperación | Desactivar al dueño de un turno abierto deja la caja sin un cierre administrativo disponible. |
| CAJ-05 | Media | Control de inventario | Retirar o cancelar devuelve automáticamente el stock sin distinguir producto devuelto de producto consumido. |
| CAJ-06 | Media | Integridad en base de datos | La restricción de datos de pago admite valores NULL aunque el pago esté marcado completo. |
| CAJ-07 | Media | Red / reintentos | Una respuesta perdida no se reconcilia claramente con el cajero; ciertos reintentos fallan después del cierre. |
| CAJ-08 | Media | Operación con lector | Los escaneos durante una petición en curso pueden perderse. |
| CAJ-09 | Media | Cobranza | CXC carece de autorización crediticia, límites y de un ciclo de recuperación del saldo. |
| CAJ-10 | Media | Conciliación bancaria | Una captura manual basta para marcar tarjeta como pagada; no existe conciliación con la terminal. |
| CAJ-11 | Media | Disponibilidad / rendimiento | Se cargan cuentas y líneas completas; faltan límites y paginación para turnos grandes y cortes. |
| CAJ-12 | Baja operativa | Interfaz | El filtro de estado de cuentas no cambia la cuenta activa del panel de venta. |
| CAJ-13 | Media operativa | Correcciones | No existe reverso autorizado de una cuenta cobrada por error ni devolución posterior al cobro. |
| CAJ-14 | Baja / Media | Diagnóstico y red | Faltan tiempo máximo de espera y mensajes específicos para conflictos de Cajas. |

Las prioridades de control operativo no implican que exista acceso anónimo ni una vulnerabilidad explotable desde Internet. Identifican formas en que una sesión autorizada o un error cotidiano puede afectar ventas, stock o conciliación.

## CAJ-01. El cajero puede otorgarse la cortesía de una venta

La política `ClientPolicy` incluye a Cajero y se aplica por igual al alta, edición y eliminación de clientes. `ClientService.UpdateAsync` acepta la categoría enviada por quien tiene esa política, sin una autorización adicional para CORTESIA.

Al abrir una cuenta, Cajas copia la categoría del catálogo. Para CORTESIA, sus líneas conservan el precio de referencia y descuentan inventario, pero el total queda en cero. Por tanto, un cajero puede cambiar un cliente de NORMAL a CORTESIA, abrir su cuenta, registrar productos, cerrar sin cobrar y devolver posteriormente la categoría del cliente a NORMAL. El alta rápida de Cajas fija NORMAL correctamente, pero el catálogo ofrece otra vía para el mismo usuario.

**Consecuencia:** la persona que realiza el cobro también puede autorizar que ese cobro sea gratuito. La validación de gratuidad de `PayAsync` no evita este escenario porque confía en la categoría que ya fue copiada.

**Corrección propuesta:** separar permisos para crear clientes normales, editar datos de contacto y cambiar categorías financieras. Restringir CORTESIA/CONVENIO a un rol autorizado o aprobación de supervisor; guardar responsable, motivo y vigencia. El servidor debe aplicar la regla aunque el formulario oculte el campo.

Referencias: [políticas](../backend/HAQPOS.Api/Program.cs#L97), [endpoints de clientes](../backend/HAQPOS.Api/Clients/ClientEndpoints.cs#L24), [cambio de categoría](../backend/HAQPOS.Infrastructure/Clients/ClientService.cs#L97), [copia de categoría](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L90), [gratuidad](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L259).

## CAJ-02. La auditoría no permite reconstruir bien los cambios financieros

Los eventos `client.created`, `client.updated` y `client.deleted` guardan actor, fecha y acción, pero no el identificador del cliente ni los valores anterior/nuevo. Un cambio a CORTESIA y su restauración a NORMAL dejan dos eventos genéricos; no queda identificado qué cliente cambió ni la transición de categoría.

Los eventos de Cajas sí guardan el GUID de cuenta/turno como `SubjectId`. Sin embargo, `UserService.AuditAsync` interpreta siempre ese campo como un usuario y lo une con `Users`. Para eventos de caja el vínculo no coincide y el DTO del visor ya no expone el GUID original. Tampoco hay una referencia directa al renglón retirado en el evento general; las líneas y movimientos conservan parte de la evidencia, pero la investigación requiere cruzar tablas manualmente.

**Corrección propuesta:** auditoría con tipo de entidad, identificador, folio, usuario, acción, motivo, valores antes/después e identificador de petición. Mostrar y filtrar eventos de caja en el visor. Conservar cambios de categoría y autorización de cortesías como eventos financieros específicos.

Referencias: [auditoría de clientes](../backend/HAQPOS.Infrastructure/Clients/ClientService.cs#L101), [auditoría de caja](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L483), [proyección del visor](../backend/HAQPOS.Infrastructure/Users/UserService.cs#L340).

## CAJ-03. El corte no incluye arqueo del dinero físico

`CloseShiftAsync` recibe el identificador del turno y lo cierra si no existen cuentas abiertas. El modelo conserva fondo inicial y ventas, pero no efectivo contado, denominaciones, diferencia, retiros, depósitos, dinero entregado ni quién lo recibió. `ExpectedCash` suma fondo y venta neta en efectivo.

El cálculo es correcto para ese modelo, pero un corte puede cerrarse con un faltante físico sin que el sistema tenga un campo donde detectarlo. Cualquier retiro de efectivo también haría que el esperado dejara de representar lo que debe existir en el cajón.

**Corrección propuesta:** cierre con contado real y diferencia calculada; motivo y autorización para diferencias; entradas/salidas extraordinarias con comprobante y aprobación; entrega/recepción identificada. Guardar el resultado del arqueo como hecho financiero del turno.

Referencias: [cierre](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L311), [modelo](../backend/HAQPOS.Domain/Cash/CashRecords.cs#L3), [totales del turno](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L457), [formulario de cierre](../frontend/haqpos-web/src/pages/cajas-page.tsx#L404).

## CAJ-04. Falta recuperación de un turno cuyo dueño ya no puede operar

Administración permite desactivar a un usuario con turno abierto. Esa acción revoca sus sesiones y `OperatingActorAsync` exige usuario activo. A la vez, `CloseShiftAsync` y las escrituras exigen que el operador sea el dueño de la caja, aunque quien intenta recuperar el turno sea administrador o supervisor. El cambio de rol o área también se bloquea mientras el turno permanezca abierto.

**Consecuencia:** ante baja, suspensión, ausencia o cuenta inutilizable, no hay un flujo de sustitución o cierre de emergencia. Reactivar temporalmente la identidad original sería una solución manual que no sustituye una autorización de recuperación y complica la responsabilidad del corte.

**Corrección propuesta:** recuperación explícita por rol autorizado, con motivo, actor que intervino, tratamiento de cuentas pendientes y conservación del cajero original. No resolverlo permitiendo que cualquier administrador modifique libremente todas las cuentas.

Referencias: [desactivación y restricciones de cambio](../backend/HAQPOS.Infrastructure/Users/UserService.cs#L220), [validación de operador](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L392), [cierre por propietario](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L315).

## CAJ-05. Cancelación financiera y devolución física están unidas

El dueño de la caja puede retirar líneas y cancelar cuentas abiertas indicando un motivo. En ambos casos, los insumos regresan automáticamente al stock; no se pregunta si el artículo realmente regresó físicamente. No hay aprobación independiente ni estado de merma/consumido/no recuperable.

Los bloqueos y la marca `RemovedAt` evitan devolver dos veces el mismo renglón. El problema es operativo: si el cliente ya tomó el producto o este se perdió, cancelar la cuenta registra una devolución que no ocurrió. En Menú tampoco se exige autorización para anular el consumo.

**Corrección propuesta:** distinguir devolución recuperable, merma y anulación financiera; pedir aprobación para las excepciones definidas y registrar el movimiento adecuado. Las cuentas abandonadas necesitan alertas y resolución autorizada; no deben devolver automáticamente artículos solo por antigüedad.

Referencias: [retiro](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L200), [cancelación](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L289), [restauración](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L445).

## CAJ-06. La restricción SQL de pagos completos no rechaza todos los NULL

`CK_CashAccounts_PaymentDetails` compara `AmountReceived >= Total` y `ChangeGiven = AmountReceived - Total` sin exigir `IS NOT NULL` para los campos obligatorios de efectivo. Hay expresiones semejantes para tarjeta. En PostgreSQL, un CHECK se considera satisfecho si su expresión produce verdadero **o NULL**.

**Consecuencia:** un pago marcado `PaymentDetailsComplete = true` puede conservar importes requeridos en NULL mediante una escritura directa o futura ruta de código. La API actual sí comprueba los campos antes de guardar; esta debilidad no equivale a poder omitirlos actualmente en el formulario o POST normal.

**Corrección propuesta:** hacer explícitos los `IS NOT NULL` y que la expresión completa solo acepte TRUE, respetando la excepción documentada de datos históricos incompletos. Reforzar también restricciones de importes y estados consistentes.

Referencias: [configuración](../backend/HAQPOS.Infrastructure/Persistence/CashModelConfiguration.cs#L32), [migración](../backend/HAQPOS.Infrastructure/Persistence/Migrations/20261105224531_CashPaymentWorkflow.cs#L74).

## CAJ-07. La recuperación después de una respuesta perdida es incompleta

El servidor reconoce correctamente un mismo `RequestId` para no descontar inventario dos veces. Sin embargo, si el servidor guarda un renglón y la respuesta se pierde, la interfaz muestra un error y vuelve a consultar la cuenta. `resolveCashCommands` elimina el identificador pendiente cuando encuentra ese renglón en la consulta. Si el cajero interpreta el error como una venta fallida y vuelve a intentar la misma operación, se genera otro identificador y se registra otro renglón.

Esto requiere una respuesta de red incierta y un reintento del operador después de la reconciliación automática; no es doble procesamiento de una misma clave en el servidor. Falta comunicar claramente «la operación sí se registró» y asociar ese resultado a la acción pendiente.

Además, los reintentos de pago pasan primero por `WritableShiftAsync`. Si la cuenta ya se pagó y el turno después se cerró, el mismo pago recibe conflicto por caja cerrada antes de llegar al reconocimiento de pago repetido. No se duplica el dinero, pero la respuesta puede sugerir fallo de una operación que sí quedó registrada.

**Corrección propuesta:** estado visible de operación pendiente/confirmada/rechazada, consulta del resultado por identificador y confirmación explícita antes de habilitar un nuevo intento. Reconocer comandos ya completados antes de rechazar nuevas escrituras por turno cerrado, conservando la autorización por propietario.

Referencias: [identificadores](../frontend/haqpos-web/src/hooks/use-cash-command.ts#L13), [reconciliación](../frontend/haqpos-web/src/components/cash-account-panel.tsx#L37), [manejo de error](../frontend/haqpos-web/src/components/cash-account-panel.tsx#L98), [pago](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L244), [verificación del turno](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L412).

## CAJ-08. El lector puede perder códigos mientras se procesa una venta

El campo del lector se deshabilita con `busy` y `perform` descarta otra llamada si `inFlight` ya es verdadero. No existe una cola de códigos escaneados. Un segundo escaneo durante la consulta o el alta del primero puede no capturarse; el riesgo aumenta con latencia.

**Corrección propuesta:** captura estable del lector con cola limitada por cuenta, procesamiento secuencial y confirmación por escaneo. Mostrar pendientes y avisar si no se pudo recibir un código. Mantener la protección contra dobles clics y no cambiar silenciosamente la cuenta destino de un escaneo pendiente.

Referencia: [captura y bloqueo del lector](../frontend/haqpos-web/src/components/cash-account-panel.tsx#L83).

## CAJ-09 y CAJ-10. CXC y tarjeta requieren controles complementarios

**CXC:** cualquier cliente no CORTESIA puede recibir esta forma de pago; no hay habilitación de crédito por cliente, saldo máximo, vencimiento o aprobación. La identificación del cliente tampoco distingue formalmente médico de colaborador para aplicar una regla distinta. El módulo lista la deuda, pero no tiene abonos, liquidación, reverso, antigüedad de saldos ni comprobante de cobranza. Es una funcionalidad pendiente ya documentada, no una corrupción del saldo actual.

También se permite eliminar un cliente que tiene CXC. La deuda y sus datos copiados se conservan, pero `ClientId` queda NULL; recrear el mismo GASS produce otra identidad en el catálogo. Para cobranza y futuros límites por cliente conviene conservar al deudor y desactivarlo, o impedir su eliminación mientras tenga saldo, en lugar de perder ese vínculo.

**Tarjeta:** la captura de cuatro dígitos, banco, autorización y tipo valida el formato, pero no prueba que el banco aceptó un cargo. No hay terminal, referencia bancaria verificada, conciliación ni evidencia de liquidación. Es un flujo manual conforme al alcance actual, con riesgo de registrar como pagado un comprobante erróneo o inexistente.

**Corrección propuesta:** definir elegibilidad y aprobación de CXC, vencimientos y cobros separados; para tarjeta, referencia/comprobante/terminal y conciliación diaria, o integración con el proveedor cuando se decida. No imponer unicidad global al número de autorización sin conocer las reglas del banco y terminal.

Referencias: [validaciones de pago](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L221), [consulta de CXC](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L345), [alcance declarado](CAJAS.md).

## CAJ-11. Las consultas crecen con todo el turno

`WorkspaceAsync` carga todas las cuentas del turno en cada actualización. Historial carga todas las cuentas de hasta veinte turnos para calcular sus resúmenes. El corte carga todas las cuentas y todas sus líneas; luego las distribuye recorriendo las líneas por cada cuenta. Incluso con el PDF sin productos por cuenta, esa carga sigue siendo necesaria en parte para las estadísticas actuales.

Cada escritura requiere además una consulta de CSRF y, después de operar, la interfaz invalida varias consultas, algunas repetidas entre padre e hijo. La autenticación vuelve a consultar sesión/usuario y el servicio consulta al actor nuevamente. No hay límites de frecuencia específicos para Cajas; el limitador configurado protege credenciales.

**Consecuencia:** un turno grande, muchas solicitudes o descargas repetidas pueden producir lentitud y consumo elevado de memoria/CPU. No se reprodujo un ataque de saturación ni se midió capacidad máxima; es un riesgo de diseño visible en las consultas.

**Corrección propuesta:** paginar cuentas y líneas, obtener sumas/conteos en SQL, agrupar líneas por cuenta una sola vez, actualizar la caché con la respuesta ya recibida, invalidar lo necesario y limitar generación simultánea de cortes. Medir tiempos de conexión, consultas, espera de bloqueos y renderizado antes de atribuir toda demora a una causa.

Referencias: [workspace](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L22), [historial](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L332), [corte](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L378), [actualización](../frontend/haqpos-web/src/pages/cajas-page.tsx#L60), [cliente HTTP](../frontend/haqpos-web/src/services/http-client.ts#L37).

## CAJ-12. El filtro y la cuenta activa pueden quedar desalineados

La lista izquierda usa `status`, pero `activeAccount` se obtiene de `selected`, de la primera cuenta abierta o de la primera cuenta global. Cambiar a Cerradas/CXC/Canceladas solo cambia `status`; no selecciona una cuenta de ese grupo ni limpia la selección anterior. Por eso el panel de la derecha puede seguir mostrando una cuenta abierta que ya no aparece en la lista filtrada.

**Consecuencia:** confusión al navegar y posible captura en una cuenta distinta de la que el cajero cree estar consultando. El servidor sigue registrando en el ID enviado y mantiene los permisos.

**Corrección propuesta:** sincronizar filtro/selección, mostrar claramente la cuenta destino y no habilitar captura en una cuenta oculta por el filtro salvo selección explícita.

Hay otra limitación con varias pestañas o dispositivos: las consultas de caja heredan `refetchOnWindowFocus: false`; solo la consulta de sesión lo habilita expresamente. Las invalidaciones de una pestaña no se comunican a las otras. Una segunda pantalla puede seguir mostrando un total anterior hasta actualizar o ejecutar una operación. La versión protege el pago contra datos desactualizados, pero no garantiza una pantalla actualizada. La documentación que indica actualización de la caja al recuperar foco debe ajustarse o implementarse. Una sincronización debe respetar el cierre por inactividad y no renovar la sesión continuamente sin interacción del operador.

Referencia: [selección y filtros](../frontend/haqpos-web/src/pages/cajas-page.tsx#L56).

## CAJ-13 y CAJ-14. Correcciones, esperas y mensajes

Una cuenta pagada no permite modificar, retirar ni cancelar líneas. Eso protege el registro original, pero falta un flujo independiente de devolución/reverso aprobado para errores reales: pago capturado con forma equivocada, devolución física o importe registrado incorrectamente. Debe generar una operación compensatoria con vínculo al original y efectos explícitos en caja, stock y CXC.

`sendJson` no tiene tiempo máximo ni cancelación de la petición. Ante una conexión que no termina, el formulario puede permanecer ocupado sin una salida clara. Agregar un timeout no basta: un timeout no demuestra que el servidor no guardó la operación; debe acompañarse de reconciliación de resultados.

El manejador general de conflictos devuelve un mensaje sobre «número de empleado» también para colisiones ajenas a usuarios. Interbloqueos, tiempos de espera y otras excepciones de PostgreSQL pueden terminar como «Ocurrió un error interno», sin una guía de recuperación en la pantalla. El servidor guarda un identificador de traza, pero el cliente solo lee el título del problema.

Referencias: [HTTP](../frontend/haqpos-web/src/services/http-client.ts#L37), [errores](../backend/HAQPOS.Api/Errors/GlobalExceptionHandler.cs#L33), [reglas de cuenta cerrada](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L473).

## Precaución para la siguiente fase de CXC

Los cortes se recalculan cada vez a partir de las cuentas. Actualmente esto conserva los importes porque la API no altera ventas de turnos cerrados. Cuando se agregue la cobranza de CXC, cambiar directamente el estado o la forma de pago de la venta original puede modificar el corte histórico o desaparecer su CXC del resumen.

La cobranza debe tener operaciones propias y distinguir el saldo actual de la deuda del hecho de que esa venta se originó como CXC en un turno anterior. Conviene conservar un resumen financiero de cierre y comparar contra sus operaciones. Cambiar el diseño del PDF no requiere modificar esos hechos.

Referencias: [reconstrucción del corte](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L378), [sumas según estado](../backend/HAQPOS.Infrastructure/Cash/CashService.cs#L457).

## Controles que sí están implementados

- La API exige sesión, contraseña actualizada y protección CSRF para las escrituras. Los permisos no dependen únicamente de ocultar botones.
- La caja pertenece al operador. Un cajero no puede escribir en turnos de otro usuario ni vender productos fuera del área del turno.
- El cajero no puede abrir caja fuera de su área asignada. Administrador/supervisor seleccionan área para su propia caja.
- Existe un índice único filtrado que impide dos turnos abiertos para el mismo usuario.
- Las ventas y devoluciones utilizan transacciones y bloqueos de filas. Se valida la existencia disponible y la base impide saldos negativos.
- Un `RequestId` repetido no descuenta dos veces el mismo renglón. Los cambios de cantidad/producto con esa misma clave se rechazan.
- Los precios se toman del catálogo en el servidor y se copian al renglón. No se aceptan precios enviados por el navegador.
- Cliente, categoría, cajero, área y producto/precio se conservan como datos de la operación; cambios posteriores del catálogo no reescriben esos datos.
- Retiro/cancelación solo restauran una vez; el cierre del turno exige que no existan cuentas abiertas.
- Pago y cancelación comprueban versión para evitar operar sobre una cuenta desactualizada; los cobros repetidos compatibles se reconocen mientras el turno sigue abierto.
- EFECTIVO comprueba cobertura y calcula cambio en servidor. CORTESIA requiere categoría compatible y total cero.
- No se guarda número completo de tarjeta ni CVV. React escapa el texto presentado y el generador PDF escapa sus cadenas. Las consultas SQL interpoladas de EF están parametrizadas.

## Verificaciones de esta revisión

Resultados detallados: `.tools/cash-review/results.log`. Las sondas específicas están fuera del proyecto normal y se incorporan únicamente al ejecutar el runner de auditoría. **Que una sonda pase significa que reprodujo el comportamiento descrito, no que lo apruebe como seguro.**

Se ejecutaron **11 comprobaciones de integración en PostgreSQL temporal: 10 pasaron y 1 falló**. Se eliminó la base temporal al terminar.

De esas 11, cinco son sondas diagnósticas añadidas solo para esta revisión; las cinco reprodujeron el resultado esperado:

| Sonda | Resultado observado |
|---|---|
| Cajero cambia cliente a CORTESIA, consume inventario y vuelve a NORMAL | Se cerró en cero y el stock bajó; los dos cambios de cliente no guardaron `SubjectId`. Confirma CAJ-01/02. |
| Desactivación del dueño de un turno | El cajero perdió autorización, el administrador no pudo cerrar ese turno ajeno y el cambio de área fue rechazado. Confirma CAJ-04. |
| Escritura de NULL en efectivo de un pago completo | PostgreSQL aceptó recibido/cambio NULL con `PaymentDetailsComplete = true`. Confirma CAJ-06 como debilidad de restricción SQL. |
| Repetir el mismo pago después de cerrar el turno | La API devolvió conflicto aunque ese pago ya estaba registrado. Confirma parte de CAJ-07. |
| Buscar MENU sobre producto MENÚ DEL DÍA | Se encontró el producto del área asignada; la consulta con traducción de acentos se ejecutó correctamente en PostgreSQL. |

Las otras seis son las pruebas existentes de Cajas: **cinco pasaron** y comprobaron área/propietario, alta NORMAL y duplicados, consumo único, pago y corte, devolución única, concurrencia sin sobreventa y conservación de datos históricos. **Una falló por una aserción del PDF desactualizada:** `CashCourtesyAccountsStayZeroKeepConsumedItemsAndCannotBeCharged`, línea 176, todavía busca la frase exacta `importe a pagar $0.00`; el diseño actual imprime `importe a pagar: $0.00`. Las comprobaciones de categoría, total cero y existencias dentro de ese escenario habían pasado antes de la aserción del documento. Hay que actualizar esa comprobación de presentación; no se debe interpretar ese fallo como un cobro indebido o como que toda la suite está aprobada.

También se ejecutaron las **18 pruebas unitarias existentes: todas pasaron**. Una sonda JavaScript con almacenamiento y hooks simulados reprodujo la eliminación de la clave de reintento al resolver un renglón por consulta y la generación de una nueva clave al repetir la acción. Esto confirma el comportamiento del identificador; el escenario completo de pérdida de respuesta y reacción del operador sigue requiriendo una prueba de interfaz con fallos de red controlados.

No se ejecutaron pruebas de saturación. Quedan por ampliar escenarios de desconexión durante COMMIT, recuperación de pestaña/equipo, lectura rápida con lector físico, cierre simultáneo con altas, interacción de cancelaciones con ajustes manuales de stock y restitución que exceda el stock máximo permitido. La presencia de transacciones es una defensa importante, pero no sustituye estas pruebas de operación y recuperación.

## Orden recomendado de corrección

1. Restringir y auditar los cambios de categoría que permiten cortesías.
2. Implementar trazabilidad financiera identificable y recuperación autorizada de turnos.
3. Agregar arqueo y separar devolución física, merma y cancelación financiera.
4. Corregir restricciones SQL y reconciliación ante respuesta perdida; adaptar la captura del lector.
5. Completar reglas y ciclo de CXC, conciliación de tarjeta y reversos autorizados.
6. Paginar, optimizar actualizaciones y medir la latencia por fase; corregir filtro/selección y mensajes.

No se realizaron operaciones de caja sobre la base de uso local ni pruebas de carga destructivas. La revisión cubre el código y los escenarios ejecutados; no constituye una auditoría de la configuración del servidor de producción, respaldos, terminal bancaria o dependencias frente a avisos externos de seguridad.
