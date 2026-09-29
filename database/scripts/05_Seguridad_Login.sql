/* =============================================================================
   VisorDatosSIG 2026
   Script 05: ampliación de seguridad - Login (PBKDF2-SHA256 + JWT)

   Contenido:
     1) dbo.Roles
     2) dbo.Usuarios
     3) dbo.UsuariosRoles
     4) dbo.Bitacora
     5) Datos mínimos: roles Administrador y Consultor, usuario admin y su relación

   Características:
     - Reproducible en una base nueva.
     - Idempotente: puede ejecutarse varias veces sin duplicar datos.
     - No modifica ni elimina registros existentes (solo inserta lo que falta).
     - NO crea ni modifica las tablas geográficas (Manzanas, Lotes, CodigosFijos, Vias).

   Esquema de contraseñas (debe coincidir con VisorDatosSIG.Infrastructure.Authentication):
     PBKDF2-SHA256, salt aleatoria de 32 bytes, 100000 iteraciones, hash de 32 bytes.
   ============================================================================= */

USE VisorDatosSIG;
GO

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   1) dbo.Roles
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        IdRol       INT IDENTITY(1,1) NOT NULL,
        NombreRol   VARCHAR(50)  NOT NULL,
        Descripcion VARCHAR(200) NULL,
        Estado      BIT          NOT NULL CONSTRAINT DF_Roles_Estado DEFAULT (1),
        CONSTRAINT PK_Roles PRIMARY KEY (IdRol)
    );
END
GO

/* ---------------------------------------------------------------------------
   2) dbo.Usuarios
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios
    (
        IdUsuario      INT IDENTITY(1,1) NOT NULL,
        Login          NVARCHAR(100) NOT NULL,
        Nombre         NVARCHAR(240) NOT NULL,
        PasswordHash   VARBINARY(32) NOT NULL,
        PasswordSalt   VARBINARY(32) NOT NULL,
        Iteraciones    INT           NOT NULL,
        Activo         BIT           NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
        FechaRegistro  DATETIME2(0)  NOT NULL CONSTRAINT DF_Usuarios_FechaRegistro DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_Usuarios PRIMARY KEY (IdUsuario)
    );
END
GO

/* ---------------------------------------------------------------------------
   3) dbo.UsuariosRoles
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.UsuariosRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsuariosRoles
    (
        IdUsuarioRol INT IDENTITY(1,1) NOT NULL,
        IdUsuario    INT NOT NULL,
        IdRol        INT NOT NULL,
        CONSTRAINT PK_UsuariosRoles PRIMARY KEY (IdUsuarioRol)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UsuariosRoles_Usuarios')
BEGIN
    ALTER TABLE dbo.UsuariosRoles
        ADD CONSTRAINT FK_UsuariosRoles_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UsuariosRoles_Roles')
BEGIN
    ALTER TABLE dbo.UsuariosRoles
        ADD CONSTRAINT FK_UsuariosRoles_Roles FOREIGN KEY (IdRol) REFERENCES dbo.Roles (IdRol);
END
GO

/* ---------------------------------------------------------------------------
   4) dbo.Bitacora
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Bitacora', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bitacora
    (
        IdBitacora BIGINT IDENTITY(1,1) NOT NULL,
        IdUsuario  INT            NULL,
        FechaHora  DATETIME2(0)   NOT NULL CONSTRAINT DF_Bitacora_FechaHora DEFAULT (SYSDATETIME()),
        Modulo     NVARCHAR(100)  NOT NULL,
        Accion     NVARCHAR(200)  NOT NULL,
        Entidad    NVARCHAR(200)  NULL,
        IdEntidad  BIGINT         NULL,
        Resultado  NVARCHAR(60)   NOT NULL,
        Detalle    NVARCHAR(MAX)  NULL,
        IP         VARCHAR(45)    NULL,
        CONSTRAINT PK_Bitacora PRIMARY KEY (IdBitacora)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Bitacora_Usuarios')
BEGIN
    ALTER TABLE dbo.Bitacora
        ADD CONSTRAINT FK_Bitacora_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario);
END
GO

/* ---------------------------------------------------------------------------
   5) Datos mínimos (idempotentes)

   Las credenciales de siembra corresponden a la contraseña de prueba indicada
   en el proyecto: Admin123!
   Se almacenan como PBKDF2-SHA256 (100000 iteraciones, hash de 32 bytes).
   En un entorno productivo este usuario debe reemplazarse o cambiarse de
   contraseña con un procedimiento autorizado.
   --------------------------------------------------------------------------- */

-- Roles
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE NombreRol = 'Administrador')
BEGIN
    INSERT INTO dbo.Roles (NombreRol, Descripcion, Estado)
    VALUES ('Administrador', 'Administrador del sistema VisorDatosSIG', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE NombreRol = 'Consultor')
BEGIN
    INSERT INTO dbo.Roles (NombreRol, Descripcion, Estado)
    VALUES ('Consultor', 'Acceso a visualizacion y consultas del VisorDatosSIG', 1);
END
GO

-- Usuario admin (solo se inserta si no existe: no se sobreescriben credenciales)
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Login = 'admin')
BEGIN
    INSERT INTO dbo.Usuarios (Login, Nombre, PasswordHash, PasswordSalt, Iteraciones, Activo, FechaRegistro)
    VALUES
    (
        'admin',
        'Administrador',
        0xD26853A2FC032A9D1081113118D545852915DA9F70E2E30BC38BD186473BC39F, -- PBKDF2-SHA256 de Admin123!
        0xA65A322F14A1CB879EA3D63822D9E8A73176A8DF24CE6376268451B1F12BA771, -- salt aleatoria de 32 bytes
        100000,
        1,
        SYSDATETIME()
    );
END
GO

-- Relación admin -> Administrador
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.UsuariosRoles AS ur
    INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = ur.IdUsuario
    INNER JOIN dbo.Roles AS r ON r.IdRol = ur.IdRol
    WHERE u.Login = 'admin' AND r.NombreRol = 'Administrador'
)
BEGIN
    INSERT INTO dbo.UsuariosRoles (IdUsuario, IdRol)
    SELECT u.IdUsuario, r.IdRol
    FROM dbo.Usuarios AS u
    CROSS JOIN dbo.Roles AS r
    WHERE u.Login = 'admin' AND r.NombreRol = 'Administrador';
END
GO

/* ---------------------------------------------------------------------------
   Verificación
   --------------------------------------------------------------------------- */
SELECT 'Roles' AS Entidad, COUNT(*) AS Filas FROM dbo.Roles
UNION ALL SELECT 'Usuarios', COUNT(*) FROM dbo.Usuarios
UNION ALL SELECT 'UsuariosRoles', COUNT(*) FROM dbo.UsuariosRoles
UNION ALL SELECT 'Bitacora', COUNT(*) FROM dbo.Bitacora;
GO

SELECT u.IdUsuario, u.Login, u.Nombre, u.Activo, u.Iteraciones,
       DATALENGTH(u.PasswordHash) AS BytesHash, DATALENGTH(u.PasswordSalt) AS BytesSalt,
       r.NombreRol
FROM dbo.Usuarios AS u
LEFT JOIN dbo.UsuariosRoles AS ur ON ur.IdUsuario = u.IdUsuario
LEFT JOIN dbo.Roles AS r ON r.IdRol = ur.IdRol
ORDER BY u.IdUsuario;
GO
