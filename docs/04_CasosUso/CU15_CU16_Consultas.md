# CU15 — Consultar Lotes y CU16 — Consultar Vías

Actores: Administrador, Supervisor, Empleado y Consultor, sujetos a permisos dinámicos de consulta.

Precondiciones: sesión válida; menú y permisos configurados; datos migrados en SQL Server.

Flujo:
1. Abrir Consultas → Lotes o Vías.
2. Introducir filtros opcionales y pulsar Buscar.
3. La API valida identidad, permiso y paginación; el repositorio combina filtros con AND.
4. Mostrar una página de resultados y su total.
5. Seleccionar Ver detalle para consultar atributos y dibujar la geometría real.
6. Limpiar restaura los filtros y la primera página.

CU15: número de lote (coincidencia parcial) e ID de manzana (exacto).
CU16: nombre, tipo de vía y OSMID (coincidencias parciales).
No se cargan geometrías en el listado; se solicitan únicamente para el detalle.

Alternativas: sin resultados; registro eliminado (404); sesión expirada (401);
acceso denegado (403); filtros inválidos (400); fallo de conexión con opción de repetir la búsqueda.
La falta de geometría no impide consultar atributos. Si falla OpenStreetMap, se conserva la geometría.

El detalle conserva Polygon/MultiPolygon para lotes y LineString/MultiLineString para vías,
en WGS 84/SRID 4326 y orden [longitud, latitud], sin inventar centroides ni coordenadas.

La consulta registra una entrada en bitácora. Un fallo de auditoría no bloquea el resultado.
No hay operaciones de creación, edición ni eliminación de datos geográficos.

Referencias: Modulos SIG.pdf (numeración vigente) y guías adjuntas.
El archivo MODULOS SIG.txt del repositorio contiene numeración anterior.
