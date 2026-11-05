# Diagnóstico de Inventario y Áreas

Revisión del código y de la base local el 4 de noviembre de 2026.

## Comportamiento revisado

- Cada insumo pertenece a un área y las consultas, ediciones y movimientos comprueban esa área en el servidor. La clave foránea impide asociar un insumo a un área inexistente.
- Los movimientos bloquean el registro durante la operación, guardan la existencia anterior y posterior en una transacción e impiden una salida superior a la existencia.
- Todos los roles pueden consultar Inventario, Menú y Áreas; solo administrador y supervisor pueden modificar Inventario y Menú. Los cajeros también pueden registrar áreas con la configuración actual.
- Se corrigió la actualización del selector de áreas después de registrar una nueva y el estado de Inventario al navegar entre áreas.
- El Menú ahora se consulta y administra por área. Los productos anteriores quedan pendientes de asignación para que un administrador o supervisor indique su área.

## Hallazgos y decisiones pendientes

1. **El mismo código físico no puede registrarse en dos áreas.** La unicidad global solicitada para códigos de barras impide tener el mismo refresco con su código original en dos inventarios. Para manejar el mismo producto en varias áreas sin inventar códigos distintos, conviene un catálogo único de productos y existencias separadas por área. No existe aún un traslado entre áreas que registre ambas operaciones de forma conjunta.
2. **La unidad de un insumo puede cambiar después de registrar movimientos.** El sistema no convierte automáticamente cajas a piezas y el historial muestra la unidad actual del insumo. Si se cambia de pieza a caja, las cantidades anteriores pueden interpretarse de forma equivocada. Conviene guardar la unidad de cada movimiento o impedir el cambio cuando ya existe historial.
3. **Los insumos de versiones anteriores deben revisarse.** La migración inicial de Inventario por área asignó los registros antiguos a la primera área creada, sin información sobre su ubicación real. Se debe confirmar su área antes de depender de esos saldos.
4. **El permiso para crear áreas incluye a los cajeros.** Es el comportamiento actual del servidor y de la interfaz. Conviene confirmar si debe reservarse a administradores y supervisores; consultar áreas sigue permitido para todos los roles.
5. **La auditoría de cambios de catálogo es limitada.** Registra la acción y el usuario, pero no identifica el insumo o producto ni conserva el precio anterior y el nuevo. Los movimientos de existencias sí guardan el insumo, el motivo y los saldos.
6. **El selector de áreas tiene un límite de 500.** Las áreas posteriores no aparecen en los selectores de Inventario, Menú y asignación de cajeros. Conviene paginar o buscar áreas si el directorio crece.

## Verificación

- Pruebas de integración con PostgreSQL aislado: separación de menús e inventarios por área, rechazo de edición en otra área, control de permisos, asignación de productos anteriores y rechazo de salidas sin existencia suficiente.
- Pruebas unitarias y compilación de la aplicación ejecutadas correctamente.
