# Base de datos

Motor: SQL Server 2022.

Los scripts deben respetar exactamente el diseño físico oficial proporcionado por el docente. No modificar nombres, claves, relaciones ni restricciones oficiales sin autorización.

## Scripts

| Script | Contenido |
|---|---|
| `01_CrearBD.sql` | Creación de la base de datos (pendiente de completar según el diseño oficial) |
| `02_Objetos.sql` | Tablas, restricciones, relaciones e índices de los objetos geográficos (pendiente de completar) |
| `03_Seguridad.sql` | Marcador de posición: no define objetos de seguridad |
| `04_DatosPrueba.sql` | Datos mínimos de prueba (pendiente de completar) |
| `05_Seguridad_Login.sql` | **Única fuente oficial** de `dbo.Usuarios`, `dbo.Roles`, `dbo.UsuariosRoles`, `dbo.Bitacora` y de los datos mínimos de seguridad |
| `VisorSIG.sql` | Script consolidado del docente: base `VisorDatosSIG`, tablas `Manzanas`, `Lotes`, `CodigosFijos`, `Vias` y trigger de `FechaCambioEstado` |

Ningún script crea ni modifica las tablas de seguridad además de `05_Seguridad_Login.sql`.

`05_Seguridad_Login.sql` es idempotente y convergente: puede ejecutarse varias veces sobre la misma
base sin duplicar registros, sin alterar las credenciales existentes y sin volver a crear las tablas
geográficas. Si detectara datos duplicados, los reporta y omite la restricción `UNIQUE`
correspondiente: nunca elimina datos automáticamente.

