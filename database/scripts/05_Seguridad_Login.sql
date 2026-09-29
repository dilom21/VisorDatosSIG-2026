/* =============================================================================
   VisorDatosSIG 2026
   Script 05: seguridad y login (PBKDF2-SHA256 + JWT)

   FUENTE OFICIAL ÚNICA de las tablas y de los datos de seguridad:
     1) dbo.Roles
     2) dbo.Usuarios
     3) dbo.UsuariosRoles
     4) dbo.Bitacora
     5) Datos mínimos: roles Administrador y Consultor, usuario admin y su relación

   Las tablas geográficas (Manzanas, Lotes, CodigosFijos, Vias) NO se crean ni se
   modifican aquí: pertenecen a VisorSIG.sql / 02_Objetos.sql.

   Características:
     - Reproducible en una base nueva y convergente en una base ya existente.
     - Idempotente: puede ejecutarse varias veces sin duplicar datos.
     - Solo agrega lo que falta (restricciones, índices y datos mínimos): no
       elimina registros ni sobrescribe credenciales existentes.
     - Si hubiera datos duplicados, se reportan y NO se eliminan: la restricción
       UNIQUE correspondiente se omite y se informa el motivo.

   Restricciones e índices oficiales (verificados contra la base local
   VisorDatosSIG en LAPTOP-470US7DH\SQLMULTI, que ya los tenía creados):
     dbo.Roles:         PK_Roles, UQ_Roles_NombreRol (NombreRol), DF_Roles_Estado
     dbo.Usuarios:      PK_Usuarios, UQ_Usuarios_Login (Login), DF_Usuarios_Activo,
                        DF_Usuarios_FechaRegistro, DF_Usuarios_Iteraciones,
                        CK_Usuarios_Iteraciones (Iteraciones > 0)
     dbo.UsuariosRoles: PK_UsuariosRoles, UQ_UsuariosRoles (IdUsuario, IdRol),
                        FK_UsuariosRoles_Usuarios, FK_UsuariosRoles_Roles
     dbo.Bitacora:      PK_Bitacora, DF_Bitacora_FechaHora,
                        FK_Bitacora_Usuarios (ON DELETE SET NULL),
                        IX_Bitacora_IdUsuario_FechaHora, IX_Bitacora_Modulo_Accion

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
        CONSTRAINT PK_Roles PRIMARY KEY (IdRol),
        CONSTRAINT UQ_Roles_NombreRol UNIQUE (NombreRol)
    );
END
GO

/* NombreRol es único: la restricción se agrega solo si falta y si no hay duplicados.
   La columna usa una intercalación que NO distingue mayúsculas ni minúsculas
   (Modern_Spanish_CI_AS), por eso el chequeo previo compara con LOWER(). */
IF NOT EXISTS
(
    SELECT 1 FROM sys.key_constraints
    WHERE name = 'UQ_Roles_NombreRol' AND parent_object_id = OBJECT_ID('dbo.Roles')
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Roles GROUP BY LOWER(LTRIM(RTRIM(NombreRol))) HAVING COUNT(*) > 1)
    BEGIN
        PRINT 'AVISO: no se creó UQ_Roles_NombreRol porque dbo.Roles tiene NombreRol duplicados.';
        PRINT 'AVISO: no se eliminó ningún registro; revise y consolide los roles manualmente.';
        SELECT LOWER(LTRIM(RTRIM(NombreRol))) AS NombreRolDuplicado, COUNT(*) AS Repeticiones
        FROM dbo.Roles
        GROUP BY LOWER(LTRIM(RTRIM(NombreRol)))
        HAVING COUNT(*) > 1;
    END
    ELSE
    BEGIN
        ALTER TABLE dbo.Roles ADD CONSTRAINT UQ_Roles_NombreRol UNIQUE (NombreRol);
        PRINT 'OK: UQ_Roles_NombreRol creada.';
    END
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
        Iteraciones    INT           NOT NULL CONSTRAINT DF_Usuarios_Iteraciones DEFAULT (100000),
        Activo         BIT           NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
        FechaRegistro  DATETIME2(0)  NOT NULL CONSTRAINT DF_Usuarios_FechaRegistro DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_Usuarios PRIMARY KEY (IdUsuario),
        CONSTRAINT UQ_Usuarios_Login UNIQUE (Login),
        CONSTRAINT CK_Usuarios_Iteraciones CHECK (Iteraciones > 0)
    );
END
GO

/* Valor por omisión oficial de Iteraciones (100000): se agrega solo si la columna
   no tiene ningún valor por omisión. Si ya tuviera otro, se conserva y se informa. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.Usuarios')
      AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Usuarios'), 'Iteraciones', 'ColumnId')
)
BEGIN
    ALTER TABLE dbo.Usuarios
        ADD CONSTRAINT DF_Usuarios_Iteraciones DEFAULT (100000) FOR Iteraciones;
    PRINT 'OK: DF_Usuarios_Iteraciones creada.';
END
ELSE IF NOT EXISTS
(
    SELECT 1 FROM sys.default_constraints
    WHERE name = 'DF_Usuarios_Iteraciones' AND parent_object_id = OBJECT_ID('dbo.Usuarios')
)
BEGIN
    PRINT 'NOTA: dbo.Usuarios.Iteraciones ya tiene un valor por omisión con otro nombre; se conserva.';
END
GO

/* Iteraciones debe ser mayor que cero: se agrega solo si falta y si no hay filas inválidas. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Usuarios_Iteraciones' AND parent_object_id = OBJECT_ID('dbo.Usuarios')
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Iteraciones <= 0)
    BEGIN
        PRINT 'AVISO: no se creó CK_Usuarios_Iteraciones porque existen filas con Iteraciones <= 0.';
        PRINT 'AVISO: no se modificó ningún dato; corríjalas manualmente y vuelva a ejecutar el script.';
        SELECT IdUsuario, Login, Iteraciones FROM dbo.Usuarios WHERE Iteraciones <= 0;
    END
    ELSE
    BEGIN
        ALTER TABLE dbo.Usuarios ADD CONSTRAINT CK_Usuarios_Iteraciones CHECK (Iteraciones > 0);
        PRINT 'OK: CK_Usuarios_Iteraciones creada.';
    END
END
GO

/* Login es único: se agrega solo si falta y si no hay duplicados.
   La columna no distingue mayúsculas ni minúsculas, por eso el chequeo usa LOWER(). */
IF NOT EXISTS
(
    SELECT 1 FROM sys.key_constraints
    WHERE name = 'UQ_Usuarios_Login' AND parent_object_id = OBJECT_ID('dbo.Usuarios')
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Usuarios GROUP BY LOWER(LTRIM(RTRIM(Login))) HAVING COUNT(*) > 1)
    BEGIN
        PRINT 'AVISO: no se creó UQ_Usuarios_Login porque dbo.Usuarios tiene Login duplicados.';
        PRINT 'AVISO: no se eliminó ningún registro; revise y consolide los usuarios manualmente.';
        SELECT LOWER(LTRIM(RTRIM(Login))) AS LoginDuplicado, COUNT(*) AS Repeticiones
        FROM dbo.Usuarios
        GROUP BY LOWER(LTRIM(RTRIM(Login)))
        HAVING COUNT(*) > 1;
    END
    ELSE
    BEGIN
        ALTER TABLE dbo.Usuarios ADD CONSTRAINT UQ_Usuarios_Login UNIQUE (Login);
        PRINT 'OK: UQ_Usuarios_Login creada.';
    END
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
        CONSTRAINT PK_UsuariosRoles PRIMARY KEY (IdUsuarioRol),
        CONSTRAINT UQ_UsuariosRoles UNIQUE (IdUsuario, IdRol)
    );
END
GO

/* Un usuario no puede tener el mismo rol dos veces: la restricción se agrega
   solo si falta y si no hay pares (IdUsuario, IdRol) repetidos. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.key_constraints
    WHERE name = 'UQ_UsuariosRoles' AND parent_object_id = OBJECT_ID('dbo.UsuariosRoles')
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.UsuariosRoles GROUP BY IdUsuario, IdRol HAVING COUNT(*) > 1)
    BEGIN
        PRINT 'AVISO: no se creó UQ_UsuariosRoles porque hay pares (IdUsuario, IdRol) repetidos.';
        PRINT 'AVISO: no se eliminó ningún registro; revise y consolide las relaciones manualmente.';
        SELECT IdUsuario, IdRol, COUNT(*) AS Repeticiones
        FROM dbo.UsuariosRoles
        GROUP BY IdUsuario, IdRol
        HAVING COUNT(*) > 1;
    END
    ELSE
    BEGIN
        ALTER TABLE dbo.UsuariosRoles ADD CONSTRAINT UQ_UsuariosRoles UNIQUE (IdUsuario, IdRol);
        PRINT 'OK: UQ_UsuariosRoles creada.';
    END
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

/* Al eliminar un usuario, la bitácora histórica se conserva y su IdUsuario queda en NULL
   (ON DELETE SET NULL). Si la restricción faltaba, se crea con ese comportamiento; si
   existía con otro comportamiento, se recrea la restricción (no se toca ningún dato). */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Bitacora_Usuarios')
BEGIN
    ALTER TABLE dbo.Bitacora
        ADD CONSTRAINT FK_Bitacora_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario) ON DELETE SET NULL;
    PRINT 'OK: FK_Bitacora_Usuarios creada (ON DELETE SET NULL).';
END
ELSE IF EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_Bitacora_Usuarios' AND delete_referential_action <> 2 -- 2 = SET NULL
)
BEGIN
    PRINT 'AVISO: FK_Bitacora_Usuarios existía sin ON DELETE SET NULL; se recrea la restricción para alinearla al diseño oficial.';
    ALTER TABLE dbo.Bitacora DROP CONSTRAINT FK_Bitacora_Usuarios;
    ALTER TABLE dbo.Bitacora
        ADD CONSTRAINT FK_Bitacora_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario) ON DELETE SET NULL;
    PRINT 'OK: FK_Bitacora_Usuarios recreada (ON DELETE SET NULL).';
END
GO

/* Índices de consulta de la bitácora (no afectan los datos existentes). */
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Bitacora_IdUsuario_FechaHora' AND object_id = OBJECT_ID('dbo.Bitacora')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Bitacora_IdUsuario_FechaHora ON dbo.Bitacora (IdUsuario, FechaHora);
    PRINT 'OK: IX_Bitacora_IdUsuario_FechaHora creado.';
END
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Bitacora_Modulo_Accion' AND object_id = OBJECT_ID('dbo.Bitacora')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Bitacora_Modulo_Accion ON dbo.Bitacora (Modulo, Accion);
    PRINT 'OK: IX_Bitacora_Modulo_Accion creado.';
END
GO

/* ---------------------------------------------------------------------------
   5) Datos mínimos (idempotentes)

   Las credenciales de siembra corresponden a la contraseña de prueba indicada
   en el proyecto: Admin123!
   Se almacenan como PBKDF2-SHA256 (100000 iteraciones, hash de 32 bytes).
   Si el usuario 'admin' ya existe, sus credenciales NO se modifican: la fila se
   conserva tal como está (hash, salt, iteraciones, estado y fecha de registro).
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

/* Restricciones e índices presentes en las cuatro tablas de seguridad.
   Deben aparecer los elementos listados en el encabezado de este script. */
SELECT t.name COLLATE DATABASE_DEFAULT AS Tabla, 'PK/UQ' AS Categoria,
       kc.name COLLATE DATABASE_DEFAULT AS Elemento,
       (CASE kc.type WHEN 'PK' THEN 'PRIMARY KEY' WHEN 'UQ' THEN 'UNIQUE' ELSE kc.type END) COLLATE DATABASE_DEFAULT AS Detalle
FROM sys.key_constraints AS kc
INNER JOIN sys.tables AS t ON t.object_id = kc.parent_object_id
WHERE t.name IN ('Usuarios', 'Roles', 'UsuariosRoles', 'Bitacora')
UNION ALL
SELECT t.name COLLATE DATABASE_DEFAULT, 'FK', fk.name COLLATE DATABASE_DEFAULT,
       fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT
FROM sys.foreign_keys AS fk
INNER JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
WHERE t.name IN ('Usuarios', 'Roles', 'UsuariosRoles', 'Bitacora')
UNION ALL
SELECT t.name COLLATE DATABASE_DEFAULT, 'CHECK', cc.name COLLATE DATABASE_DEFAULT, 'CHECK'
FROM sys.check_constraints AS cc
INNER JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
WHERE t.name IN ('Usuarios', 'Roles', 'UsuariosRoles', 'Bitacora')
UNION ALL
SELECT t.name COLLATE DATABASE_DEFAULT, 'DEFAULT', dc.name COLLATE DATABASE_DEFAULT,
       c.name COLLATE DATABASE_DEFAULT
FROM sys.default_constraints AS dc
INNER JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
INNER JOIN sys.tables AS t ON t.object_id = dc.parent_object_id
WHERE t.name IN ('Usuarios', 'Roles', 'UsuariosRoles', 'Bitacora')
UNION ALL
SELECT t.name COLLATE DATABASE_DEFAULT, 'INDICE', i.name COLLATE DATABASE_DEFAULT,
       i.type_desc COLLATE DATABASE_DEFAULT
FROM sys.indexes AS i
INNER JOIN sys.tables AS t ON t.object_id = i.object_id
WHERE t.name = 'Bitacora' AND i.name IS NOT NULL AND i.is_primary_key = 0
ORDER BY Tabla, Categoria, Elemento;
GO

