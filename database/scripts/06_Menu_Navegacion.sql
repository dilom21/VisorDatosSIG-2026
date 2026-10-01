/* =============================================================================
   VisorDatosSIG 2026
   Script 06: menú de navegación del sistema (CU01 - menú dinámico)

   FUENTE OFICIAL ÚNICA de la tabla y de los datos del menú:
     1) dbo.MenuOpciones
     2) Datos mínimos: los módulos web del sistema con sus opciones.

   Consumido por:
     - VisorDatosSIG.Infrastructure.Navigation.MenuRepository -> opciones con Estado = 1
     - VisorDatosSIG.Api.Controllers.MenuController           -> GET /api/menu
     - VisorDatosSIG.Web (js/navigation/sidebar.js)           -> menú lateral dinámico

   Las tablas geográficas (Manzanas, Lotes, CodigosFijos, Vias) pertenecen a
   VisorSIG.sql; las de seguridad (Roles, Usuarios, UsuariosRoles, Bitacora) a
   05_Seguridad_Login.sql. Ninguna se toca aquí.

   Esquema (verificado contra la base VisorDatosSIG de Azure SQL que ya lo tenía):
     dbo.MenuOpciones: PK_MenuOpciones, FK_MenuOpciones_Padre (autorreferencia),
                       DF_MenuOpciones_Orden, DF_MenuOpciones_Estado
     Columnas: IdMenu (IDENTITY), IdMenuPadre (NULL = primer nivel), Nivel,
               NombreMenu, Url (NULL = agrupador), Icono, Orden, Estado

   Características:
     - Reproducible en una base nueva y convergente en una base ya existente.
     - Idempotente: puede ejecutarse varias veces sin duplicar opciones.
     - Solo agrega lo que falta: no elimina ni sobrescribe opciones existentes.
     - Los enlaces se identifican por Url (su ruta real) y los agrupadores por
       (IdMenuPadre, NombreMenu): el script no depende de valores IDENTITY.
     - El orden de inserción reproduce los mismos IdMenu de la base de referencia
       (1 a 12), de modo que una base nueva queda idéntica.
     - Ninguna ruta ni icono se inventa: las Url corresponden a los controladores
       reales de VisorDatosSIG.Web y las claves de Icono existen en el catálogo
       controlado de js/navigation/sidebar.js (_IconosSidebar.cshtml).
   ============================================================================= */

USE VisorDatosSIG;
GO

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   1) dbo.MenuOpciones
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.MenuOpciones', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MenuOpciones
    (
        IdMenu      INT IDENTITY(1,1) NOT NULL,
        IdMenuPadre INT           NULL,
        Nivel       INT           NOT NULL,
        NombreMenu  NVARCHAR(200) NOT NULL,
        Url         NVARCHAR(400) NULL,
        Icono       NVARCHAR(100) NULL,
        Orden       INT           NOT NULL CONSTRAINT DF_MenuOpciones_Orden DEFAULT (0),
        Estado      BIT           NOT NULL CONSTRAINT DF_MenuOpciones_Estado DEFAULT (1),
        CONSTRAINT PK_MenuOpciones PRIMARY KEY (IdMenu)
    );
    PRINT 'OK: dbo.MenuOpciones creada.';
END
GO

/* Una base ya existente podría tener la tabla sin la columna Nivel: se agrega y
   se rellena a partir de la jerarquía (1 = primer nivel, 2 = hijas, ...) sin
   tocar ninguna fila existente más que ese relleno. */
IF COL_LENGTH('dbo.MenuOpciones', 'Nivel') IS NULL
BEGIN
    ALTER TABLE dbo.MenuOpciones ADD Nivel INT NULL;
    PRINT 'NOTA: dbo.MenuOpciones.Nivel no existía; se agregó como NULL.';
END
GO

IF EXISTS
(
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.MenuOpciones')
      AND name = 'Nivel'
      AND is_nullable = 1
)
BEGIN
    UPDATE dbo.MenuOpciones SET Nivel = 1 WHERE IdMenuPadre IS NULL AND Nivel IS NULL;

    DECLARE @pendientes INT = 1;

    WHILE @pendientes > 0
    BEGIN
        UPDATE h
           SET h.Nivel = p.Nivel + 1
        FROM dbo.MenuOpciones AS h
        INNER JOIN dbo.MenuOpciones AS p ON p.IdMenu = h.IdMenuPadre
        WHERE h.Nivel IS NULL
          AND p.Nivel IS NOT NULL;

        SET @pendientes = @@ROWCOUNT;
    END

    PRINT 'OK: Nivel rellenado en dbo.MenuOpciones.';
END
GO

/* La autorreferencia se agrega solo si falta (una base existente podría tenerla
   con otro nombre). IdMenuPadre NULL identifica un menú de primer nivel. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.MenuOpciones')
      AND referenced_object_id = OBJECT_ID('dbo.MenuOpciones')
)
BEGIN
    ALTER TABLE dbo.MenuOpciones
        ADD CONSTRAINT FK_MenuOpciones_Padre FOREIGN KEY (IdMenuPadre)
        REFERENCES dbo.MenuOpciones (IdMenu);
    PRINT 'OK: FK_MenuOpciones_Padre creada.';
END
GO


/* ---------------------------------------------------------------------------
   2) Datos mínimos del menú (solo se insertan si no existen)

   El orden de inserción es el mismo en que se crearon las opciones en la base de
   referencia, para que una base nueva obtenga los mismos IdMenu (1 a 12).
   --------------------------------------------------------------------------- */

/* 1) Usuarios y Seguridad (agrupador: Url NULL) */
IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE IdMenuPadre IS NULL AND NombreMenu = N'Usuarios y Seguridad')
BEGIN
    INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
    VALUES (NULL, 1, N'Usuarios y Seguridad', NULL, 'shield', 1, 1);
    PRINT 'OK: agrupador Usuarios y Seguridad agregado.';
END
GO

DECLARE @seguridad INT =
(
    SELECT IdMenu FROM dbo.MenuOpciones
    WHERE IdMenuPadre IS NULL AND NombreMenu = N'Usuarios y Seguridad'
);

IF @seguridad IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Usuarios')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@seguridad, 2, N'Gestionar Usuarios', '/Usuarios', 'users', 1, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Roles')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@seguridad, 2, N'Gestionar Roles y Permisos', '/Roles', 'user-shield', 2, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Bitacora')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@seguridad, 2, N'Consultar Bitácora', '/Bitacora', 'history', 3, 1);

    PRINT 'OK: opciones de Usuarios y Seguridad verificadas.';
END
ELSE
BEGIN
    PRINT 'AVISO: no se encontró el agrupador Usuarios y Seguridad; no se agregaron sus opciones.';
END
GO

/* 2) Gestionar Mapa (enlace directo al visor cartográfico) */
IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Visor')
BEGIN
    INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
    VALUES (NULL, 1, N'Gestionar Mapa', '/Visor', 'map', 2, 1);
    PRINT 'OK: opción Gestionar Mapa agregada.';
END
GO

/* 3) Consultas y Filtros (agrupador) */
IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE IdMenuPadre IS NULL AND NombreMenu = N'Consultas y Filtros')
BEGIN
    INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
    VALUES (NULL, 1, N'Consultas y Filtros', NULL, 'search', 3, 1);
    PRINT 'OK: agrupador Consultas y Filtros agregado.';
END
GO

DECLARE @consultas INT =
(
    SELECT IdMenu FROM dbo.MenuOpciones
    WHERE IdMenuPadre IS NULL AND NombreMenu = N'Consultas y Filtros'
);

IF @consultas IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Consultas/CodigoFijo')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@consultas, 2, N'Consultar Código Fijo', '/Consultas/CodigoFijo', 'map-pin', 1, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Consultas/Manzana')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@consultas, 2, N'Consultar Manzana', '/Consultas/Manzana', 'grid', 2, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Consultas/Lotes')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@consultas, 2, N'Consultar Lotes', '/Consultas/Lotes', 'layers', 3, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Consultas/Vias')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@consultas, 2, N'Consultar Vías', '/Consultas/Vias', 'route', 4, 1);

    PRINT 'OK: opciones de Consultas y Filtros verificadas.';
END
ELSE
BEGIN
    PRINT 'AVISO: no se encontró el agrupador Consultas y Filtros; no se agregaron sus opciones.';
END
GO

/* 4) Reportes (agrupador con una opción de gestión) */
IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE IdMenuPadre IS NULL AND NombreMenu = N'Reportes')
BEGIN
    INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
    VALUES (NULL, 1, N'Reportes', NULL, 'chart', 4, 1);
    PRINT 'OK: agrupador Reportes agregado.';
END
GO

DECLARE @reportes INT =
(
    SELECT IdMenu FROM dbo.MenuOpciones
    WHERE IdMenuPadre IS NULL AND NombreMenu = N'Reportes'
);

IF @reportes IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Reportes')
        INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
        VALUES (@reportes, 2, N'Gestionar Reportes', '/Reportes', 'file-chart', 1, 1);

    PRINT 'OK: opciones de Reportes verificadas.';
END
ELSE
BEGIN
    PRINT 'AVISO: no se encontró el agrupador Reportes; no se agregaron sus opciones.';
END
GO

/* ---------------------------------------------------------------------------
   3) Comprobación final: jerarquía servida por GET /api/menu (el servicio
      ordena por Orden y desempata por IdMenu, igual que este SELECT)
   --------------------------------------------------------------------------- */
SELECT
    p.NombreMenu AS Agrupador,
    h.NombreMenu AS Opcion,
    h.Url,
    h.Icono,
    h.Orden      AS OrdenOpcion
FROM dbo.MenuOpciones AS p
LEFT JOIN dbo.MenuOpciones AS h ON h.IdMenuPadre = p.IdMenu AND h.Estado = 1
WHERE p.IdMenuPadre IS NULL
  AND p.Estado = 1
ORDER BY p.Orden, p.IdMenu, h.Orden, h.IdMenu;
GO

SELECT 'Opciones activas' AS Entidad, COUNT(*) AS Filas FROM dbo.MenuOpciones WHERE Estado = 1
UNION ALL SELECT 'Opciones totales', COUNT(*) FROM dbo.MenuOpciones;
GO
