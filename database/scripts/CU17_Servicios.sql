SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @IdMenuPadre INT;
DECLARE @IdMenuServicios INT;

SELECT TOP (1) @IdMenuPadre = IdMenu
FROM dbo.MenuOpciones
WHERE NombreMenu = N'Seguimiento de Servicios'
ORDER BY IdMenu;

IF @IdMenuPadre IS NULL
BEGIN
    THROW 50001, 'No se encontro el menu padre Seguimiento de Servicios.', 1;
END;

SELECT TOP (1) @IdMenuServicios = IdMenu
FROM dbo.MenuOpciones
WHERE Url = N'/Servicios'
  AND IdMenuPadre = @IdMenuPadre
ORDER BY IdMenu;

IF @IdMenuServicios IS NULL
BEGIN
    INSERT INTO dbo.MenuOpciones
        (IdMenuPadre, NombreMenu, Url, Icono, Orden, Estado)
    VALUES
        (@IdMenuPadre, N'Gestionar Servicios', N'/Servicios', N'water_drop', 0, 1);

    SET @IdMenuServicios = CONVERT(INT, SCOPE_IDENTITY());
END;
ELSE
BEGIN
    UPDATE dbo.MenuOpciones
    SET Estado = 1
    WHERE IdMenu = @IdMenuServicios;
END;

UPDATE rm
SET PuedeVer = 1,
    PuedeCrear = 0,
    PuedeEditar = 1,
    PuedeEliminar = 0
FROM dbo.RolMenu rm
INNER JOIN dbo.Roles r ON r.IdRol = rm.IdRol
WHERE rm.IdMenu = @IdMenuServicios
  AND r.NombreRol = N'Supervisor';

INSERT INTO dbo.RolMenu (IdRol, IdMenu, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar)
SELECT r.IdRol, @IdMenuServicios, 1, 0, 1, 0
FROM dbo.Roles r
WHERE r.NombreRol = N'Supervisor'
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RolMenu rm
      WHERE rm.IdRol = r.IdRol
       AND rm.IdMenu = @IdMenuServicios
   );

UPDATE rm
SET PuedeVer = 0,
    PuedeCrear = 0,
    PuedeEditar = 0,
    PuedeEliminar = 0
FROM dbo.RolMenu rm
INNER JOIN dbo.Roles r ON r.IdRol = rm.IdRol
WHERE rm.IdMenu = @IdMenuServicios
  AND r.NombreRol IN (N'Empleado', N'Consultor');

COMMIT TRANSACTION;
GO
