/* =============================================================================
   VisorDatosSIG 2026
   Script 07: permisos por opción de menú (CU03 - Roles y CU04 - Permisos)

   FUENTE OFICIAL ÚNICA de los permisos iniciales del sistema web.

   Consumido por:
     - VisorDatosSIG.Infrastructure.Security.PermisoRepository -> autorización web
     - VisorDatosSIG.Infrastructure.Security.RolRepository     -> PUT /api/roles/{id}/permisos
     - VisorDatosSIG.Api.Controllers.RolesController           -> /api/roles

   Contexto de la base VisorDatosSIG:
     - dbo.RolMenu YA EXISTE con PK_RolMenu, UQ_RolMenu (IdRol, IdMenu) y las columnas
       PuedeVer, PuedeCrear, PuedeEditar y PuedeEliminar. Este script NO crea ni
       modifica esa tabla ni sus restricciones: solo siembra filas de permisos.
     - dbo.Roles, dbo.UsuariosRoles y dbo.Bitacora pertenecen a 05_Seguridad_Login.sql;
       dbo.MenuOpciones pertenece a 06_Menu_Navegacion.sql. Ninguna se toca aquí.

   Permisos iniciales (solo AGREGA filas que faltan; nunca borra ni sobrescribe):
     - Administrador: sin filas. Su acceso total es implícito y se resuelve por nombre de
       rol (RolesSistema.EsAdministrador), no por dbo.RolMenu.
     - Consultor: PuedeVer = 1 sobre /Visor y las cuatro consultas iniciales
       (/Consultas/CodigoFijo, /Consultas/Manzana, /Consultas/Lotes, /Consultas/Vias),
       sin crear, editar ni eliminar. No incluye bitácora, usuarios, roles ni reportes.
     - RESPONSABLE MIGRADOR / MIGRADOR: sin filas. No participa de la seguridad web y el
       inicio de sesión del Migrador sigue funcionando sin cambios.

   Características:
     - Idempotente: puede ejecutarse varias veces sin duplicar filas.
     - No revierte lo configurado después desde /api/roles/{id}/permisos.
     - No depende de valores IDENTITY: el rol se resuelve por NombreRol y la opción de menú
       por su Url (misma ruta declarada en VisorDatosSIG.Application.Common.PermisoMenu).
     - Los agrupadores no se siembran: no autorizan nada por sí mismos (el menú se arma en
       MenuService).
   ============================================================================= */

USE VisorDatosSIG;
GO

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   1) Permisos iniciales del rol 'Consultor'
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.RolMenu', 'U') IS NULL
   OR OBJECT_ID('dbo.Roles', 'U') IS NULL
   OR OBJECT_ID('dbo.MenuOpciones', 'U') IS NULL
BEGIN
    PRINT 'AVISO: falta dbo.RolMenu, dbo.Roles o dbo.MenuOpciones; no se sembraron permisos.';
    PRINT 'AVISO: ejecute primero 05_Seguridad_Login.sql y 06_Menu_Navegacion.sql.';
END
ELSE
BEGIN
    DECLARE @IdRolConsultor INT =
    (
        SELECT MIN(IdRol) FROM dbo.Roles WHERE NombreRol = 'Consultor'
    );

    IF @IdRolConsultor IS NULL
    BEGIN
        PRINT 'AVISO: no existe el rol Consultor; ejecute 05_Seguridad_Login.sql antes que este script.';
    END
    ELSE
    BEGIN
        INSERT INTO dbo.RolMenu (IdRol, IdMenu, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar)
        SELECT @IdRolConsultor, m.IdMenu, 1, 0, 0, 0
        FROM dbo.MenuOpciones AS m
        WHERE m.Estado = 1
          AND m.Url IN ('/Visor',
                        '/Consultas/CodigoFijo',
                        '/Consultas/Manzana',
                        '/Consultas/Lotes',
                        '/Consultas/Vias')
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.RolMenu AS rm
              WHERE rm.IdRol = @IdRolConsultor
                AND rm.IdMenu = m.IdMenu
          );

        DECLARE @FilasNuevas INT = @@ROWCOUNT;

        PRINT CONCAT(
            'OK: permisos iniciales del rol Consultor verificados; se agregaron ',
            @FilasNuevas,
            ' fila(s) nuevas.');
    END
END
GO

/* ---------------------------------------------------------------------------
   2) Verificación
   --------------------------------------------------------------------------- */
SELECT r.IdRol,
       r.NombreRol,
       r.Estado         AS RolActivo,
       COUNT(rm.IdMenu) AS OpcionesConPermiso
FROM dbo.Roles AS r
LEFT JOIN dbo.RolMenu AS rm ON rm.IdRol = r.IdRol
GROUP BY r.IdRol, r.NombreRol, r.Estado
ORDER BY r.NombreRol;
GO

SELECT m.IdMenu,
       m.NombreMenu,
       m.Url,
       rm.PuedeVer,
       rm.PuedeCrear,
       rm.PuedeEditar,
       rm.PuedeEliminar
FROM dbo.RolMenu AS rm
INNER JOIN dbo.Roles AS r ON r.IdRol = rm.IdRol
INNER JOIN dbo.MenuOpciones AS m ON m.IdMenu = rm.IdMenu
WHERE r.NombreRol = 'Consultor'
ORDER BY m.Orden, m.IdMenu;
GO
