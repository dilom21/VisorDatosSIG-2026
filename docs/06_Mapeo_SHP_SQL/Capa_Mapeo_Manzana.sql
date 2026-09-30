
--MAPEO DE LA PRIMERA CAPA SHP MANZANA


--Origen = shapefile de donde vienen los datos
--Destino = mapeo donde se iran los datos 


/* CAMPOS ORIGEN SHP

MANZANA
Id
UV_MZA
UV
MZA
geometría

*/








-- MAPEO PARA EL DESTINO DE MANZANAS

/*
============================================================
MAPEO SHP -> SQL SERVER : CAPA MANZANAS
============================================================

ARCHIVO ORIGEN:
    Exp_MapaBase_MZA_4326.shp

TABLA DESTINO:
    dbo.Manzanas

CORRESPONDENCIA DE CAMPOS:

    SHP / DBF               SQL SERVER
    ---------------------------------------------------------
    Id              --->    IdOrigen
                             Regla: si Id = 0, guardar NULL

    UV_MZA          --->    UV_MZA
                             Mapeo directo
                             Permite NULL

    UV              --->    UV
                             Mapeo directo
                             Permite NULL

    MZA             --->    MZA
                             Mapeo directo
                             Permite NULL

    Geometria SHP   --->    Geom
                             Tipo SQL: geometry
                             SRID: 4326

CAMPO GENERADO POR SQL SERVER:

    IdManzana
        - No proviene del SHP.
        - Se genera automáticamente mediante IDENTITY.
        - Será la clave primaria de dbo.Manzanas.

REGLAS IMPORTANTES:

    1. El campo Id del SHP NO debe utilizarse como clave primaria,
       debido a que los registros analizados contienen Id = 0.

    2. IdOrigen conservará el identificador de origen únicamente
       cuando sea diferente de 0.

    3. Los valores NULL existentes en UV_MZA, UV y MZA
       deben conservarse como NULL.

    4. MZA no debe tener restricción UNIQUE porque sus valores
       pueden repetirse entre diferentes sectores o unidades vecinales.

    5. La geometría debe almacenarse con SRID 4326.

REGISTROS ESPERADOS:
    863

GEOMETRIAS VALIDAS:
    863

GEOMETRIAS INVALIDAS:
    0

============================================================
*/










-- 2) RESTRICCIONES


-- RESTRICCIONES DE LA CAPA MANZANAS

/*
============================================================
RESTRICCIONES : dbo.Manzanas
============================================================

1. IdManzana
   - PRIMARY KEY.
   - Se genera automáticamente mediante IDENTITY.
   - Identifica de forma única cada registro dentro de SQL Server.

2. IdOrigen
   - Permite NULL.
   - No se define como UNIQUE porque en el SHP analizado
     el campo Id contiene 0 en todos los registros.
   - Durante la migración, Id = 0 se transformará a NULL.

3. UV_MZA
   - Permite NULL.
   - No se define como UNIQUE porque existen valores repetidos
     y registros sin información.

4. UV
   - Permite NULL.
   - No se define como UNIQUE.

5. MZA
   - Permite NULL.
   - No se define como UNIQUE porque el número de manzana
     puede repetirse en distintas unidades vecinales.

6. Geom
   - Tipo geometry.
   - Las geometrías migradas deberán utilizar SRID 4326.
   - En el archivo analizado se encontraron 863 geometrías válidas
     y ninguna geometría inválida.

============================================================
*/












-- 3) INDICES	


-- INDICES DE LA CAPA MANZANAS

/*
============================================================
INDICES : dbo.Manzanas
============================================================

1. Índice normal para búsquedas por UV y MZA.
2. Índice espacial para consultas sobre la geometría.
============================================================
*/



CREATE INDEX IX_Manzanas_UV_MZA ON dbo.Manzanas(UV, MZA);




CREATE SPATIAL INDEX SIX_Manzanas_Geom
ON dbo.Manzanas(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);