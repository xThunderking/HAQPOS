# Cajas

El turno de caja pertenece a un usuario y a un área. Cada cajero opera exclusivamente en su área asignada; el servidor comprueba esta relación en cada operación. Los administradores y supervisores pueden abrir su propio turno seleccionando un área registrada y consultar cortes de otros cajeros. Consultor y Almacenista conservan acceso al módulo sin permisos de venta.

## Flujo

1. Abrir caja con un fondo inicial en MXN, que puede ser cero. Un usuario solo puede tener un turno abierto.
2. Abrir tantas cuentas como necesite seleccionando un cliente registrado. El alta rápida utiliza número GASS / colaborador alfanumérico de hasta diez caracteres, nombre sin números y consultorio o Externo. Su categoría es NORMAL y el servidor la fija; el identificador del cliente sigue siendo único en todo el catálogo.
3. Buscar productos del inventario o del Menú del área por nombre o código. Un código exacto seguido de Enter agrega el producto: admite un lector que se comporte como teclado.
4. Agregar cantidades enteras de piezas. El precio lo obtiene el servidor del catálogo, sin aceptar precios enviados desde el navegador. Los insumos se descuentan inmediatamente; los productos del Menú solo generan el renglón de cuenta.
5. Retirar renglones o cancelar cuentas abiertas requiere un motivo y devuelve las piezas de inventario una sola vez. Se conserva el renglón original y el motivo. Una cuenta cerrada no admite estas modificaciones.
6. Los clientes CORTESIA conservan los productos y el consumo de inventario, pero su importe a pagar permanece en $0.00. Se cierra la cuenta como cortesía sin registrar un cobro.
7. Para EFECTIVO, el cajero captura lo recibido; la pantalla calcula el cambio. El corte conserva recibido, cambio y venta neta. Solo la venta neta aumenta el efectivo esperado.
8. Para TARJETA se registran últimos cuatro dígitos, autorización, banco y tipo crédito/débito. No se guarda el número completo de tarjeta ni el CVV.
9. Para CXC, la cuenta se libera en caja y queda como pendiente de pago. Aparece en el módulo Cuentas por cobrar, filtrado por propietario para cajeros y visible entre áreas para administradores y supervisores. También aparece en el corte como CXC.
10. Cerrar caja requiere que no queden cuentas abiertas. El corte conserva cuentas cobradas, cortesías, CXC pendientes, productos y anulaciones; se puede volver a consultar, descargar como PDF o abrir en el visor del navegador para imprimir.

## Totales y reglas pendientes

El corte separa EFECTIVO, CXC pendiente y TARJETA. El total registrado suma las ventas efectivas y las cuentas CXC; excluye las cuentas CORTESIA, cuyo total es cero. Efectivo esperado = fondo inicial + ventas en efectivo. El recibido y el cambio quedan reportados aparte. CXC y tarjeta no incrementan el efectivo esperado.

El cierre de CXC registra la obligación pendiente, pero todavía no incluye cobro, abono ni cancelación desde el módulo Cuentas por cobrar. La conexión con una terminal bancaria tampoco forma parte de este flujo: el cajero captura el resultado que emitió la terminal. No se almacena el PAN completo ni el CVV de la tarjeta.

## PDF del corte

La primera hoja contiene el resumen consolidado: datos del turno, ventas registradas, cobros realizados, CXC pendiente, cuentas por forma de pago, crédito/débito, cortesías, cancelaciones, fondo inicial, recibido, cambio y efectivo esperado. También muestra el consumo de inventario y Menú, el valor de referencia de las cortesías y los productos retirados.

Después comienzan en páginas propias las secciones EFECTIVO, CXC y TARJETA. Tarjeta agrupa crédito y débito, e incluye banco, autorización y últimos cuatro dígitos. Cortesías y cancelaciones tienen sus propios apartados cuando existen. Cada cuenta muestra folio, cliente, identificador, consultorio o Externo, fechas, datos de pago y total. El PDF no incluye el desglose de productos consumidos por cuenta; el inventario, consumo del Menú y productos retirados se mantienen en los totales resumidos de la primera hoja.

Las cuentas extensas continúan en otras páginas repitiendo folio, cliente y encabezados de tabla. El PDF incluye numeración, índice de secciones, marcadores de navegación y espacios de firma.

El documento se genera desde los datos guardados cada vez que se descarga o imprime. Esto aplica también a los cortes históricos, sin modificar ventas ni importes. Los datos de pago que no se capturaron en versiones anteriores se identifican como «Sin dato». Para obtener el nuevo diseño de un PDF ya descargado, hay que volver a descargarlo desde Cortes anteriores.

## Integridad

- Turnos, cuentas y renglones tienen identificadores de solicitud para reconocer reintentos sin crear dos cuentas ni descontar dos veces. El navegador conserva las solicitudes pendientes mientras se resuelve una respuesta de red incierta.
- Las operaciones bloquean al cajero, su turno, la cuenta y los insumos dentro de transacciones de PostgreSQL. Dos cajeros que intenten vender las últimas piezas no pueden producir un saldo negativo.
- No se permite cerrar un turno con cuentas abiertas ni cambiar el rol o área de un usuario con caja abierta. La desactivación de seguridad sigue disponible.
- Los datos de cliente, cajero, área, nombre del producto y precio quedan copiados al registrar la operación. Las ediciones posteriores del catálogo no cambian las cuentas históricas. Eliminar un cliente conserva sus datos en el corte.
- Las escrituras requieren sesión activa, contraseña actualizada, autorización en servidor y protección CSRF. Se registran eventos de auditoría y movimientos de inventario con el folio de cuenta.
- La pantalla se actualiza con las operaciones, al recuperar el foco o mediante actualización manual. No realiza consultas periódicas que mantengan una sesión activa sin interacción del cajero.
- Los recibos y cuentas se autorizan por propietario; administradores y supervisores pueden consultar los cortes. El acceso por identificador no permite modificar turnos de otros usuarios.
- Los datos de pago se validan en el servidor y la base de datos: el efectivo recibido debe cubrir el total y el cambio coincide con la resta; tarjeta exige autorización, banco, tipo y solo los últimos cuatro dígitos. El único cierre sin cobro es CORTESIA para clientes con esa categoría.

## Comprobación

Se agregaron seis pruebas de integración con PostgreSQL en una base temporal: aislamiento por área y propietario, categoría NORMAL y duplicados de clientes, descuento único y cobro, devolución única al retirar/cancelar, ventas concurrentes sin sobreventa, conservación de datos históricos, CORTESIA en cero y manejo de CXC. Las pruebas del PDF comprueban resumen en una hoja, orden de secciones, inclusión de todas las cuentas, omisión del desglose de productos, datos históricos incompletos, límites de página, caracteres en español y referencias internas del archivo. Los ejemplos se renderizaron con el visor PDF de Windows para revisar su diseño.

La interfaz se revisó con datos simulados en escritorio y móvil, incluyendo escaneo con Enter, cambio de cuenta, alta rápida y bloqueo del cierre con cuentas pendientes. Estas comprobaciones no crean ventas en la base de uso local.
