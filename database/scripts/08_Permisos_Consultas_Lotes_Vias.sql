/*
CU15 y CU16: permisos iniciales para roles web existentes.
Ejecutar despues de 05, 06 y 07. No crea roles ni modifica permisos existentes.
Administrador mantiene su acceso implicito.
*/
USE VisorDatosSIG;
GO
SET NOCOUNT ON;
IF OBJECT_ID('dbo.RolMenu','U') IS NULL OR OBJECT_ID('dbo.Roles','U') IS NULL
   OR OBJECT_ID('dbo.MenuOpciones','U') IS NULL
    THROW 50001, 'Ejecute primero los scripts de seguridad y menu.', 1;
INSERT INTO dbo.RolMenu (IdRol,IdMenu,PuedeVer,PuedeCrear,PuedeEditar,PuedeEliminar)
SELECT r.IdRol,m.IdMenu,1,0,0,0
FROM dbo.Roles r
CROSS JOIN dbo.MenuOpciones m
WHERE r.NombreRol IN (N'Supervisor',N'Empleado',N'Consultor')
  AND m.Estado=1 AND m.Url IN ('/Consultas/Lotes','/Consultas/Vias')
  AND NOT EXISTS (SELECT 1 FROM dbo.RolMenu rm WHERE rm.IdRol=r.IdRol AND rm.IdMenu=m.IdMenu);
PRINT CONCAT('Permisos nuevos: ', @@ROWCOUNT);
GO
