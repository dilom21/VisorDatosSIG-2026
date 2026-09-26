# VisorDatosSIG.Api

Capa HTTP para exponer autenticación, catálogo de capas, GeoJSON, búsquedas, filtros e historial autorizado.

Endpoints previstos:
- GET /api/capas
- GET /api/capas/{capa}/geojson
- GET /api/capas/{capa}/{id}
- GET /api/busqueda
- POST /api/autenticacion/iniciar
- GET /api/migraciones

No debe exponer credenciales, cadenas de conexión ni trazas internas.
