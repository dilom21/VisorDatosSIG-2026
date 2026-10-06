/* CU18 - Gestionar Bajas Parciales
   Re-runnable, data-preserving deployment script. */

IF OBJECT_ID(N'dbo.BajasParciales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BajasParciales
    (
        IdBajaParcial INT IDENTITY(1,1) NOT NULL,
        IdEmpleado INT NOT NULL,
        FechaInicio DATE NOT NULL,
        FechaFin DATE NOT NULL,
        Motivo NVARCHAR(300) NOT NULL,
        Observaciones NVARCHAR(500) NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_BajasParciales_Estado DEFAULT (N'Activa'),
        FechaRegistro DATETIME2 NOT NULL CONSTRAINT DF_BajasParciales_FechaRegistro DEFAULT (SYSDATETIME()),
        FechaModificacion DATETIME2 NULL,
        DisponibilidadAnterior NVARCHAR(50) NULL,
        CONSTRAINT PK_BajasParciales PRIMARY KEY (IdBajaParcial)
    );
END;
GO

IF COL_LENGTH(N'dbo.BajasParciales', N'DisponibilidadAnterior') IS NULL
BEGIN
    ALTER TABLE dbo.BajasParciales
        ADD DisponibilidadAnterior NVARCHAR(50) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_BajasParciales_Empleados'
      AND parent_object_id = OBJECT_ID(N'dbo.BajasParciales')
)
BEGIN
    ALTER TABLE dbo.BajasParciales WITH CHECK
        ADD CONSTRAINT FK_BajasParciales_Empleados
        FOREIGN KEY (IdEmpleado) REFERENCES dbo.Empleados(IdEmpleado);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_BajasParciales_Fechas'
      AND parent_object_id = OBJECT_ID(N'dbo.BajasParciales')
)
BEGIN
    ALTER TABLE dbo.BajasParciales WITH CHECK
        ADD CONSTRAINT CK_BajasParciales_Fechas
        CHECK (FechaFin >= FechaInicio);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_BajasParciales_Estado'
      AND parent_object_id = OBJECT_ID(N'dbo.BajasParciales')
)
BEGIN
    ALTER TABLE dbo.BajasParciales WITH CHECK
        ADD CONSTRAINT CK_BajasParciales_Estado
        CHECK (Estado IN (N'Activa', N'Finalizada', N'Cancelada'));
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_BajasParciales_EmpleadoFechas'
      AND object_id = OBJECT_ID(N'dbo.BajasParciales')
)
BEGIN
    CREATE INDEX IX_BajasParciales_EmpleadoFechas
        ON dbo.BajasParciales (IdEmpleado, FechaInicio, FechaFin)
        INCLUDE (Estado, DisponibilidadAnterior);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_BajasParciales_Estado'
      AND object_id = OBJECT_ID(N'dbo.BajasParciales')
)
BEGIN
    CREATE INDEX IX_BajasParciales_Estado
        ON dbo.BajasParciales (Estado, FechaFin, FechaInicio);
END;
GO

SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @IdMenuPadre INT;
DECLARE @IdMenuCorrecto INT;

SELECT TOP (1) @IdMenuPadre = IdMenu
FROM dbo.MenuOpciones
WHERE NombreMenu = N'Seguimiento de Servicios'
ORDER BY IdMenu;

IF @IdMenuPadre IS NULL
BEGIN
    THROW 50001, 'No se encontro el menu padre Seguimiento de Servicios.', 1;
END;

SELECT TOP (1) @IdMenuCorrecto = IdMenu
FROM dbo.MenuOpciones
WHERE IdMenuPadre = @IdMenuPadre
  AND NombreMenu = N'Gestionar Bajas Parciales'
  AND Url = N'/Servicios/BajasParciales'
ORDER BY IdMenu;

IF @IdMenuCorrecto IS NULL
BEGIN
    INSERT INTO dbo.MenuOpciones
        (IdMenuPadre, NombreMenu, Url, Icono, Orden, Estado)
    VALUES
        (@IdMenuPadre, N'Gestionar Bajas Parciales', N'/Servicios/BajasParciales', N'event_busy', 0, 1);

    SET @IdMenuCorrecto = CONVERT(INT, SCOPE_IDENTITY());
END;
ELSE
BEGIN
    UPDATE dbo.MenuOpciones
    SET Estado = 1
    WHERE IdMenu = @IdMenuCorrecto;
END;

UPDATE rm
SET PuedeVer = 1,
    PuedeCrear = 1,
    PuedeEditar = 1,
    PuedeEliminar = 0
FROM dbo.RolMenu rm
INNER JOIN dbo.Roles r ON r.IdRol = rm.IdRol
WHERE rm.IdMenu = @IdMenuCorrecto
  AND r.NombreRol IN (N'Supervisor', N'Empleado');

INSERT INTO dbo.RolMenu (IdRol, IdMenu, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar)
SELECT r.IdRol, @IdMenuCorrecto, 1, 1, 1, 0
FROM dbo.Roles r
WHERE r.NombreRol IN (N'Supervisor', N'Empleado')
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RolMenu rm
      WHERE rm.IdRol = r.IdRol
        AND rm.IdMenu = @IdMenuCorrecto
  );

DELETE rm
FROM dbo.RolMenu rm
INNER JOIN dbo.MenuOpciones mo ON mo.IdMenu = rm.IdMenu
WHERE mo.Url = N'/BajasParciales'
  AND mo.IdMenu <> @IdMenuCorrecto;

DELETE FROM dbo.MenuOpciones
WHERE Url = N'/BajasParciales'
  AND IdMenu <> @IdMenuCorrecto;

COMMIT TRANSACTION;
GO
