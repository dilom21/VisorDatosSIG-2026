
CREATE DATABASE VisorDatosSIG;
GO

USE VisorDatosSIG;
GO

/*
============================================================
TABLA: dbo.Manzanas

Origen:
    Exp_MapaBase_MZA_4326.shp

La PK IdManzana es generada por SQL Server.
IdOrigen almacena el Id del SHP cuando sea útil.
La geometría se almacenará en Geom.
============================================================
*/

CREATE TABLE dbo.Manzanas
(
    IdManzana INT IDENTITY(1,1) PRIMARY KEY,
    IdOrigen INT NULL,
    UV_MZA NVARCHAR(20) NULL,
    UV NVARCHAR(15) NULL,
    MZA NVARCHAR(10) NULL,
    Geom geometry NULL
);
GO


-- =========================================================
-- INDICES DE dbo.Manzanas
-- =========================================================

-- Facilita búsquedas y filtros por Unidad Vecinal + Manzana.
-- NO es UNIQUE porque estos valores pueden repetirse.
CREATE INDEX IX_Manzanas_UV_MZA
ON dbo.Manzanas(UV, MZA);
GO

-- Índice espacial para consultas sobre la geometría:
-- intersecciones, contención, relaciones con lotes, etc.
CREATE SPATIAL INDEX SIX_Manzanas_Geom
ON dbo.Manzanas(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);
GO



------------------------------------------------------------------------------------------------------------------



/*
============================================================
TABLA: dbo.Lotes

Origen:
    Exp_MapaBase_LOTES_4326.shp

IdLote:
    PK generada automáticamente por SQL Server.

IdOrigen:
    Recibirá el Id del SHP.
    Durante la migración, Id = 0 se convertirá en NULL.

NroLote:
    Número de lote proveniente del DBF.
    NO es único.

IdManzana:
    FK hacia dbo.Manzanas.
    Su valor se obtendrá posteriormente mediante
    relación espacial.

Geom:
    Geometría proveniente del SHP.
============================================================
*/

CREATE TABLE dbo.Lotes
(
    IdLote INT IDENTITY(1,1) PRIMARY KEY,
    IdOrigen INT NULL,
    NroLote NVARCHAR(15) NULL,
    IdManzana INT NULL,
    Geom geometry NULL,

    CONSTRAINT FK_Lotes_Manzanas
        FOREIGN KEY (IdManzana)
        REFERENCES dbo.Manzanas(IdManzana)
);
GO


-- =========================================================
-- INDICES DE dbo.Lotes
-- =========================================================

-- Índice para búsquedas por número de lote.
CREATE INDEX IX_Lotes_NroLote
ON dbo.Lotes(NroLote);
GO

-- Índice recomendado para acelerar relaciones con Manzanas.
CREATE INDEX IX_Lotes_IdManzana
ON dbo.Lotes(IdManzana);
GO

-- Índice espacial para consultas geométricas.
CREATE SPATIAL INDEX SIX_Lotes_Geom
ON dbo.Lotes(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);
GO




------------------------------------------------------------------------------------------------------------------


/*
============================================================
TABLA: dbo.CodigosFijos

Origen:
    Exp_CodigoFijo_4326.shp

IMPORTANTE:
    - Text no se migrará porque duplica CodF_SIG.
    - Longitud y Latitud se obtendrán desde la geometría.
    - IdLote se determinará mediante relación espacial.
    - CodFijo NO es clave única.
============================================================
*/

CREATE TABLE dbo.CodigosFijos
(
    IdCodigo INT IDENTITY(1,1) PRIMARY KEY,

    CodF_SQL INT NULL,
    CodF_SIG NVARCHAR(25) NULL,
    CodFijo INT NULL,
    Nombre NVARCHAR(120) NULL,

    Estado TINYINT NOT NULL
        CONSTRAINT DF_CodigosFijos_Estado DEFAULT(1),

    FechaCambioEstado DATETIME2 NOT NULL
        CONSTRAINT DF_CodigosFijos_FechaCambioEstado
        DEFAULT(SYSDATETIME()),

    IdLote INT NULL,

    Longitud FLOAT NULL,
    Latitud FLOAT NULL,

    Geom geometry NULL,

    CONSTRAINT CK_CodigosFijos_Estado
        CHECK (Estado BETWEEN 1 AND 5),

    CONSTRAINT FK_CodigosFijos_Lotes
        FOREIGN KEY (IdLote)
        REFERENCES dbo.Lotes(IdLote)
);
GO




-- =========================================================
-- INDICES DE dbo.CodigosFijos
-- =========================================================

CREATE INDEX IX_CodigosFijos_CodFijo
ON dbo.CodigosFijos(CodFijo);
GO

CREATE INDEX IX_CodigosFijos_Nombre
ON dbo.CodigosFijos(Nombre);
GO

CREATE INDEX IX_CodigosFijos_Estado
ON dbo.CodigosFijos(Estado);
GO

CREATE INDEX IX_CodigosFijos_IdLote
ON dbo.CodigosFijos(IdLote);
GO

CREATE SPATIAL INDEX SIX_CodigosFijos_Geom
ON dbo.CodigosFijos(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);
GO



-- =========================================================
-- TRIGGER PARA ACTUALIZAR FechaCambioEstado
-- =========================================================

CREATE OR ALTER TRIGGER dbo.TR_CodigosFijos_FechaCambioEstado
ON dbo.CodigosFijos
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(Estado)
        RETURN;

    UPDATE c
       SET FechaCambioEstado = SYSDATETIME()
    FROM dbo.CodigosFijos c
    JOIN inserted i
      ON i.IdCodigo = c.IdCodigo
    JOIN deleted d
      ON d.IdCodigo = i.IdCodigo
    WHERE i.Estado <> d.Estado;
END;
GO




------------------------------------------------------------------------------------------------------------------




/*
============================================================
TABLA: dbo.Vias

Origen:
    Exp_MapaBase_VIAS_4326.shp

MAPEO PRINCIPAL:
    OBJECTID  -> OBJECTID
    name      -> Nombre
    type      -> TipoVia
    osm_id    -> OSMID
    geometría -> Geom

Campos redundantes o no utilizados:
    Nombre, OSMID, ref, oneway,
    bridge, maxspeed, highway
============================================================
*/

CREATE TABLE dbo.Vias
(
    IdVia INT IDENTITY(1,1) PRIMARY KEY,
    OBJECTID INT NULL,
    Nombre NVARCHAR(40) NULL,
    TipoVia NVARCHAR(30) NULL,
    OSMID NVARCHAR(20) NULL,
    Geom geometry NULL
);
GO


USE VisorDatosSIG;
GO

-- =========================================================
-- INDICES DE dbo.Vias
-- =========================================================

-- Búsquedas por identificador OSM.
CREATE INDEX IX_Vias_OSMID
ON dbo.Vias(OSMID);
GO

-- Búsquedas por nombre de vía.
CREATE INDEX IX_Vias_Nombre
ON dbo.Vias(Nombre);
GO

-- Filtros por tipo de vía.
CREATE INDEX IX_Vias_TipoVia
ON dbo.Vias(TipoVia);
GO

-- Consultas espaciales sobre la geometría.
CREATE SPATIAL INDEX SIX_Vias_Geom
ON dbo.Vias(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);
GO



