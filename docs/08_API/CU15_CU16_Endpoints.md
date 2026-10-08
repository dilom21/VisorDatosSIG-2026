# API — Lotes y Vías

Todas las rutas requieren Bearer JWT y el permiso PuedeVer de su opción de menú.
El cliente usa VisorSIG.api.peticion y la sesión existente; la Web no consulta SQL directamente.

| Método | Ruta | Parámetros |
|---|---|---|
| GET | /api/lotes | nroLote, idManzana, pagina, limite |
| GET | /api/lotes/{idLote} | ID positivo |
| GET | /api/vias | nombre, tipoVia, osmid, pagina, limite |
| GET | /api/vias/{idVia} | ID positivo |

pagina: >= 1, valor inicial 1. limite: 1–100, valor inicial 20.
Filtros de texto: se recortan espacios; una cadena vacía equivale a no filtrar.
idManzana debe ser positivo. Los filtros se combinan con AND.
Consultas SQL parametrizadas y orden estable por ID, con OFFSET/FETCH.

Lista: { pagina, limite, totalRegistros, totalPaginas, datos }.
Lote: idLote, nroLote, idManzana. Detalle agrega idOrigen y geometria.
Vía: idVia, nombre, tipoVia, osmid. Detalle agrega objectid y geometria.
geometria siempre se incluye en el detalle, incluso cuando vale null.

Errores: 400 validación; 401 identidad ausente/sesión inválida;
403 permiso denegado; 404 registro inexistente.
Sin resultados devuelve 200 con datos vacío.

Ejemplos:
- /api/lotes?nroLote=12&idManzana=76&pagina=1&limite=20
- /api/vias?nombre=Avenida&tipoVia=Principal&pagina=1&limite=20

Configuración: reutiliza las tablas y opciones de menú existentes.
08_Permisos_Consultas_Lotes_Vias.sql agrega permisos de consulta faltantes para
Supervisor, Empleado y Consultor si esos roles existen. No crea roles ni cambia
permisos ya configurados. Los roles faltantes pueden administrarse en Roles.
