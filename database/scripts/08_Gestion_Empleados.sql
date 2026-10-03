/* =============================================================================
   VisorDatosSIG 2026
   Script 08: gestión de empleados (CU04 – Gestionar Empleados)

   FUENTE OFICIAL de la tabla de empleados y permisos del módulo operativo:
     1) dbo.Empleados
     2) Rol Supervisor en dbo.Roles (si no existe)
     3) Opción de menú /Empleados en dbo.MenuOpciones
     4) Permisos iniciales en dbo.RolMenu para Administrador y Supervisor
     5) Datos de siembra iniciales para actividades operativas

   Relación con otros casos de uso:
     Fuente de información básica del personal para los procesos de bajas parciales,
     disponibilidad de personal, asignación de servicios y gestión de rutas
     (módulo de Seguimiento de Servicios).
   ============================================================================= */

USE VisorDatosSIG;
GO

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   1) dbo.Empleados
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Empleados', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Empleados
    (
        IdEmpleado         INT IDENTITY(1,1) NOT NULL,
        Codigo             NVARCHAR(20)      NOT NULL,
        Nombres            NVARCHAR(100)     NOT NULL,
        Apellidos          NVARCHAR(100)     NOT NULL,
        DocumentoIdentidad NVARCHAR(30)      NOT NULL,
        Telefono           NVARCHAR(30)      NULL,
        Email              NVARCHAR(150)     NULL,
        Cargo              NVARCHAR(100)     NOT NULL,
        Area               NVARCHAR(100)     NOT NULL CONSTRAINT DF_Empleados_Area DEFAULT ('Operaciones'),
        Disponibilidad     NVARCHAR(50)      NOT NULL CONSTRAINT DF_Empleados_Disponibilidad DEFAULT ('Disponible'),
        Activo             BIT               NOT NULL CONSTRAINT DF_Empleados_Activo DEFAULT (1),
        FechaRegistro      DATETIME2(0)      NOT NULL CONSTRAINT DF_Empleados_FechaRegistro DEFAULT (SYSDATETIME()),
        FechaModificacion  DATETIME2(0)      NULL,
        Observaciones      NVARCHAR(500)     NULL,
        CONSTRAINT PK_Empleados PRIMARY KEY CLUSTERED (IdEmpleado),
        CONSTRAINT UQ_Empleados_Codigo UNIQUE (Codigo),
        CONSTRAINT UQ_Empleados_DocumentoIdentidad UNIQUE (DocumentoIdentidad)
    );
    PRINT 'OK: dbo.Empleados creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Empleados_Cargo_Disponibilidad' AND object_id = OBJECT_ID('dbo.Empleados'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Empleados_Cargo_Disponibilidad
    ON dbo.Empleados (Cargo, Disponibilidad, Activo)
    INCLUDE (Codigo, Nombres, Apellidos, Telefono);
    PRINT 'OK: IX_Empleados_Cargo_Disponibilidad creado.';
END
GO

/* ---------------------------------------------------------------------------
   2) Rol 'Supervisor' en dbo.Roles
   --------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE NombreRol = 'Supervisor')
BEGIN
    INSERT INTO dbo.Roles (NombreRol, Descripcion, Estado)
    VALUES ('Supervisor', 'Supervisión de operaciones, cuadrillas y seguimiento de servicios', 1);
    PRINT 'OK: Rol Supervisor creado.';
END
GO

/* ---------------------------------------------------------------------------
   3) Menú /Empleados en dbo.MenuOpciones
   --------------------------------------------------------------------------- */
-- Agrupador: Seguimiento de Servicios (o ubicar bajo Usuarios y Seguridad si ya existe)
DECLARE @idAgrupador INT = (
    SELECT IdMenu FROM dbo.MenuOpciones WHERE IdMenuPadre IS NULL AND NombreMenu = N'Seguimiento de Servicios'
);

IF @idAgrupador IS NULL
BEGIN
    -- Si no existe el agrupador de servicios, se crea como módulo 5
    INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
    VALUES (NULL, 1, N'Seguimiento de Servicios', NULL, 'route', 5, 1);
    SET @idAgrupador = SCOPE_IDENTITY();
    PRINT 'OK: Agrupador Seguimiento de Servicios creado.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.MenuOpciones WHERE Url = '/Empleados')
BEGIN
    INSERT INTO dbo.MenuOpciones (IdMenuPadre, Nivel, NombreMenu, Url, Icono, Orden, Estado)
    VALUES (@idAgrupador, 2, N'Gestionar Empleados', '/Empleados', 'users', 1, 1);
    PRINT 'OK: Opción Gestionar Empleados (/Empleados) agregada al menú.';
END
GO

/* ---------------------------------------------------------------------------
   4) Permisos en dbo.RolMenu para 'Supervisor'
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.RolMenu', 'U') IS NOT NULL
BEGIN
    DECLARE @idRolSupervisor INT = (SELECT IdRol FROM dbo.Roles WHERE NombreRol = 'Supervisor');
    DECLARE @idMenuEmpleados INT = (SELECT IdMenu FROM dbo.MenuOpciones WHERE Url = '/Empleados');
    DECLARE @idMenuVisor     INT = (SELECT IdMenu FROM dbo.MenuOpciones WHERE Url = '/Visor');

    IF @idRolSupervisor IS NOT NULL AND @idMenuEmpleados IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.RolMenu WHERE IdRol = @idRolSupervisor AND IdMenu = @idMenuEmpleados)
        BEGIN
            INSERT INTO dbo.RolMenu (IdRol, IdMenu, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar)
            VALUES (@idRolSupervisor, @idMenuEmpleados, 1, 1, 1, 1);
            PRINT 'OK: Permisos de Gestionar Empleados concedidos a Supervisor.';
        END
    END

    IF @idRolSupervisor IS NOT NULL AND @idMenuVisor IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.RolMenu WHERE IdRol = @idRolSupervisor AND IdMenu = @idMenuVisor)
        BEGIN
            INSERT INTO dbo.RolMenu (IdRol, IdMenu, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar)
            VALUES (@idRolSupervisor, @idMenuVisor, 1, 0, 0, 0);
            PRINT 'OK: Permiso de Visor concedido a Supervisor.';
        END
    END
END
GO

/* ---------------------------------------------------------------------------
   5) Datos de siembra de empleados operativos (idempotentes)
   --------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Empleados WHERE Codigo = 'EMP-001')
BEGIN
    INSERT INTO dbo.Empleados (Codigo, Nombres, Apellidos, DocumentoIdentidad, Telefono, Email, Cargo, Area, Disponibilidad, Activo, Observaciones)
    VALUES ('EMP-001', N'Carlos', N'Mendoza Flores', '7845123 SC', '70012345', 'cmendoza@visordatossig.com', N'Técnico de Campo', N'Operaciones', N'Disponible', 1, N'Personal asignado a inspecciones y reconexiones');
END

IF NOT EXISTS (SELECT 1 FROM dbo.Empleados WHERE Codigo = 'EMP-002')
BEGIN
    INSERT INTO dbo.Empleados (Codigo, Nombres, Apellidos, DocumentoIdentidad, Telefono, Email, Cargo, Area, Disponibilidad, Activo, Observaciones)
    VALUES ('EMP-002', N'Roberto', N'Gutierrez Paz', '6547891 SC', '71098765', 'rgutierrez@visordatossig.com', N'Supervisor de Zona', N'Seguimiento de Servicios', N'Disponible', 1, N'Supervisor de cuadrilla urbana zona norte');
END

IF NOT EXISTS (SELECT 1 FROM dbo.Empleados WHERE Codigo = 'EMP-003')
BEGIN
    INSERT INTO dbo.Empleados (Codigo, Nombres, Apellidos, DocumentoIdentidad, Telefono, Email, Cargo, Area, Disponibilidad, Activo, Observaciones)
    VALUES ('EMP-003', N'Andrés', N'Salazar Vaca', '8912345 SC', '72541236', 'asalazar@visordatossig.com', N'Conductor de Cuadrilla', N'Transporte y Rutas', N'En Servicio', 1, N'Asignado a vehículo operativo móvil V-12');
END

IF NOT EXISTS (SELECT 1 FROM dbo.Empleados WHERE Codigo = 'EMP-004')
BEGIN
    INSERT INTO dbo.Empleados (Codigo, Nombres, Apellidos, DocumentoIdentidad, Telefono, Email, Cargo, Area, Disponibilidad, Activo, Observaciones)
    VALUES ('EMP-004', N'Lucía', N'Fernández Suarez', '5412369 SC', '73654128', 'lfernandez@visordatossig.com', N'Inspectora Catastral', N'Catastro y Redes', N'Disponible', 1, N'Verificación de suministros y códigos fijos');
END

IF NOT EXISTS (SELECT 1 FROM dbo.Empleados WHERE Codigo = 'EMP-005')
BEGIN
    INSERT INTO dbo.Empleados (Codigo, Nombres, Apellidos, DocumentoIdentidad, Telefono, Email, Cargo, Area, Disponibilidad, Activo, Observaciones)
    VALUES ('EMP-005', N'Jorge', N'Justiniano Roca', '4125896 SC', '74852147', 'jjustiniano@visordatossig.com', N'Técnico de Campo', N'Operaciones', N'Baja Parcial', 1, N'Baja parcial por permiso de capacitación técnica');
END
GO

/* ---------------------------------------------------------------------------
   Verificación
   --------------------------------------------------------------------------- */
SELECT IdEmpleado, Codigo, Nombres, Apellidos, DocumentoIdentidad, Cargo, Disponibilidad, Activo
FROM dbo.Empleados
ORDER BY IdEmpleado;
GO
