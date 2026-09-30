

/*
============================================================
MAPEO SHP -> SQL SERVER : CAPA VIAS
============================================================

ARCHIVO ORIGEN:
    Exp_MapaBase_VIAS_4326.shp

TABLA DESTINO:
    dbo.Vias

SHP / DBF               SQL SERVER
---------------------------------------------------------

OBJECTID        --->    OBJECTID
                         Mapeo directo.
                         Se comprobó que contiene
                         578 valores distintos.

name            --->    Nombre
                         Mapeo directo.
                         Se comprobó que name y Nombre
                         contienen exactamente la misma
                         información en los 578 registros.

Nombre          --->    NO SE MIGRA
                        Es redundante con name.

type            --->     TipoVia
                         Mapeo directo.
                         Se detectaron 12 categorías
                         diferentes.

osm_id          --->     OSMID
                         Mapeo directo con conversión
                         de Integer64 a NVARCHAR(20).

OSMID           --->     NO SE MIGRA
                         Es redundante con osm_id.
                         Se comprobó que ambos son iguales
                         en los 578 registros.

ref             --->     NO SE MIGRA
                         Presenta 572 valores NULL de 578.
                         La tabla destino actual no posee
                         un campo correspondiente.

oneway          --->     NO SE MIGRA
                         La tabla destino actual no posee
                         un campo correspondiente.
                         Valores detectados:
                             0 = 513
                             1 = 65

bridge          --->     NO SE MIGRA
                         La tabla destino actual no posee
                         un campo correspondiente.
                         Valores detectados:
                             0 = 566
                             1 = 12

maxspeed        --->     NO SE MIGRA
                         Los 578 registros contienen 0.
                         No aporta información útil
                         en el archivo analizado.

highway         --->     NO SE MIGRA
                         El campo está completamente vacío.
                         578 valores NULL.

Geometria SHP   --->    Geom
                         Tipo SQL: geometry.
                         SRID: 4326.


CAMPO GENERADO POR SQL SERVER:

    IdVia
        - No proviene del SHP.
        - Se genera mediante IDENTITY.
        - Será la clave primaria.


REGLAS IMPORTANTES:

    1. name y Nombre son campos redundantes.
       Se utilizará name como fuente para SQL.Nombre.

    2. osm_id y OSMID son campos redundantes.
       Se utilizará osm_id como fuente para SQL.OSMID.

    3. osm_id es único dentro del archivo analizado:
           578 valores distintos
           0 repetidos.

    4. OBJECTID también es único:
           578 valores distintos
           0 repetidos.

    5. type contiene 12 categorías diferentes.

    6. highway se encuentra completamente vacío.

    7. maxspeed contiene únicamente el valor 0.

    8. No se detectaron geometrías duplicadas.

    9. La geometría debe almacenarse con SRID 4326.


REGISTROS ESPERADOS:
    578

GEOMETRIAS VALIDAS:
    578

GEOMETRIAS INVALIDAS:
    0

GEOMETRIAS DUPLICADAS:
    0

============================================================
*/





-- 2) RESTRICCIONES


-- RESTRICCIONES DE LA CAPA VIAS

/*
============================================================
RESTRICCIONES : dbo.Vias
============================================================

1. IdVia
   - PRIMARY KEY.
   - Se genera automáticamente mediante IDENTITY.
   - No proviene del SHP.

2. OBJECTID
   - Permite NULL.
   - En el archivo analizado:
         578 valores distintos
         0 repetidos.
   - No se define como UNIQUE por ahora,
     ya que solo se comprobó su unicidad
     dentro del archivo actual.

3. Nombre
   - Permite NULL.
   - Se obtiene desde el campo name del SHP.
   - Se detectaron 376 registros sin nombre.

4. TipoVia
   - Permite NULL.
   - Se obtiene desde el campo type.
   - En el archivo analizado no presenta NULL.
   - Se encontraron 12 categorías diferentes.

5. OSMID
   - Permite NULL.
   - Se obtiene desde osm_id.
   - El campo osm_id fue único en los
     578 registros analizados.
   - No se define como UNIQUE todavía,
     porque no se ha confirmado que su
     unicidad esté garantizada en futuras cargas.

6. Geom
   - Tipo geometry.
   - Debe almacenarse con SRID 4326.
   - En el archivo analizado:
         578 geometrías válidas
         0 geometrías inválidas
         0 geometrías duplicadas.

7. Los campos redundantes del SHP:
       Nombre
       OSMID
   no se utilizan como fuente porque
   contienen la misma información que:
       name
       osm_id

8. Los campos ref, oneway, bridge,
   maxspeed y highway no se almacenan
   en la tabla destino actual.

============================================================
*/







-- 3) INDICES


-- INDICES DE LA CAPA VIAS

/*
============================================================
INDICES : dbo.Vias
============================================================

INDICE DEFINIDO EN EL ESQUEMA DEL ING:

1. Índice espacial sobre Geom.

INDICES RECOMENDADOS:

2. Índice sobre OSMID para búsquedas
   por identificador proveniente de OSM.

3. Índice sobre Nombre para búsquedas
   de calles o vías por nombre.

4. Índice sobre TipoVia para filtros
   por categoría de vía.

============================================================
*/

CREATE INDEX IX_Vias_OSMID
ON dbo.Vias(OSMID);

CREATE INDEX IX_Vias_Nombre
ON dbo.Vias(Nombre);


CREATE INDEX IX_Vias_TipoVia
ON dbo.Vias(TipoVia);


CREATE SPATIAL INDEX SIX_Vias_Geom
ON dbo.Vias(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);


