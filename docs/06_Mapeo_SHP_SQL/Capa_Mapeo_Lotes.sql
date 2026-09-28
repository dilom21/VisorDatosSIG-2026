

--- CAPA DE LOTES


/* SHP LOTES ORIGEN (Exp_MapaBase_LOTES_4326)

	Id
	NroLote
	geometríA
*/




-- 1)   MAPEO PARA EL DESTINO LOTES



/*
============================================================
MAPEO SHP -> SQL SERVER : CAPA LOTES
============================================================

ARCHIVO ORIGEN:
    Exp_MapaBase_LOTES_4326.shp

TABLA DESTINO:
    dbo.Lotes

CORRESPONDENCIA DE CAMPOS:

    SHP / DBF               SQL SERVER
    ---------------------------------------------------------

    Id              --->    IdOrigen
                             Regla: si Id = 0, guardar NULL

    NroLote         --->    NroLote
                             Mapeo directo.
                             Puede contener el valor especial '0'.

    Geometria SHP   --->    Geom
                             Tipo SQL: geometry
                             SRID: 4326

CAMPO GENERADO POR SQL SERVER:

    IdLote
        - No proviene del SHP.
        - Se genera automáticamente mediante IDENTITY.
        - Será la clave primaria de dbo.Lotes.

CAMPO DERIVADO:

    IdManzana
        - No existe en el SHP de lotes.
        - Se obtiene mediante relación espacial entre
          la geometría del lote y dbo.Manzanas.Geom.
        - Será FK hacia dbo.Manzanas(IdManzana).

REGLAS IMPORTANTES:

    1. El Id del SHP no se utiliza como clave primaria.
       Los registros analizados presentan Id = 0.

    2. NroLote NO es único.
       Los números de lote pueden repetirse entre manzanas.

    3. Se encontraron 5.565 registros con NroLote = '0'.
       Por ahora se conserva este valor hasta confirmar
       si representa "sin número" o "sin información".

    4. La relación Lote -> Manzana se determinará
       mediante análisis espacial.

    5. La geometría debe almacenarse con SRID 4326.

    6. Se detectó una geometría inválida:
       NroLote = 'L11'
       Error: auto-intersección en anillo.

REGISTROS ESPERADOS:
    15.281

NroLote NULL:
    0

NroLote = '0':
    5.565

NroLote != '0':
    9.716

GEOMETRIAS VALIDAS:
    15.280

GEOMETRIAS INVALIDAS:
    1

============================================================
*/












-- 2) RESTRICCIONES


-- RESTRICCIONES DE LA CAPA LOTES

/*
============================================================
RESTRICCIONES : dbo.Lotes
============================================================

1. IdLote
   - PRIMARY KEY.
   - Se genera automáticamente mediante IDENTITY.
   - Identifica de forma única cada lote dentro de SQL Server.

2. IdOrigen
   - Permite NULL.
   - No se define como UNIQUE.
   - Durante la migración:
         Id = 0 -> NULL

3. NroLote
   - Permite NULL.
   - NO debe tener restricción UNIQUE.
   - Los números de lote se repiten entre diferentes manzanas.
   - Se detectaron 5.565 registros con NroLote = '0'.

4. IdManzana
   - Permite NULL inicialmente.
   - Será FOREIGN KEY hacia dbo.Manzanas(IdManzana).
   - Su valor no proviene directamente del SHP.
   - Se obtiene mediante relación espacial entre lote y manzana.

5. Geom
   - Tipo geometry.
   - Debe almacenarse con SRID 4326.
   - Se detectaron:
         15.280 geometrías válidas
         1 geometría inválida

6. La geometría inválida corresponde a:
       NroLote = 'L11'
       Error: auto-intersección en anillo.

   Esta geometría no debe eliminarse automáticamente.
   El migrador deberá detectarla y registrar la incidencia
   para decidir posteriormente si se corrige o se omite.

============================================================
*/






-- 3) INDICES



-- INDICES DE LA CAPA LOTES

/*
============================================================
INDICES : dbo.Lotes
============================================================

1. Índice normal sobre NroLote.
2. Índice sobre IdManzana para acelerar relaciones y búsquedas.
3. Índice espacial sobre Geom.
============================================================
*/

CREATE INDEX IX_Lotes_NroLote
ON dbo.Lotes(NroLote);


CREATE INDEX IX_Lotes_IdManzana
ON dbo.Lotes(IdManzana);



CREATE SPATIAL INDEX SIX_Lotes_Geom
ON dbo.Lotes(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);



-- LLAVE FORANEA DE MANZANA
ALTER TABLE dbo.Lotes
ADD CONSTRAINT FK_Lotes_Manzanas
FOREIGN KEY (IdManzana)
REFERENCES dbo.Manzanas(IdManzana);