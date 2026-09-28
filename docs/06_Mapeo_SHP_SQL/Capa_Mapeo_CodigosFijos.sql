

/*
============================================================
MAPEO SHP -> SQL SERVER : CAPA CODIGOS FIJOS
============================================================

ARCHIVO ORIGEN:
    Exp_CodigoFijo_4326.shp

TABLA DESTINO:
    dbo.CodigosFijos


CORRESPONDENCIA DE CAMPOS:

ORIGEN SHP / DBF                  SQL SERVER
-------------------------------------------------------------

Text                  --->        NO SE MIGRA
                                  Es redundante con CodF_SIG.
                                  Se comprobó que ambos coinciden
                                  en los 6.271 registros.


CodF_SQL              --->        CodF_SQL
                                  Mapeo directo.

                                  Tipo origen:
                                      Integer64

                                  Tipo destino:
                                      INT

                                  Antes de realizar la carga se debe
                                  validar que el valor se encuentre
                                  dentro del rango admitido por INT.


CodF_SIG              --->        CodF_SIG
                                  Mapeo directo.

                                  Se conserva el valor original
                                  proveniente del DBF.

                                  Se detectaron 5 registros con
                                  anomalías de formato relacionadas
                                  con espacios o separadores.

                                  Estas anomalías deben registrarse
                                  durante la migración y no corregirse
                                  silenciosamente.


CodFijo               --->        CodFijo
                                  Mapeo directo.

                                  NO debe utilizarse como clave
                                  primaria ni declararse UNIQUE,
                                  debido a que existen valores
                                  repetidos.


Nombre                --->        Nombre
                                  Mapeo directo.


Longi                 --->        NO UTILIZAR PARA GENERAR
                                  LA COORDENADA FINAL.

                                  Es un atributo original del DBF,
                                  pero la geometría del SHP será
                                  la fuente espacial de referencia.


Latid                 --->        NO UTILIZAR PARA GENERAR
                                  LA COORDENADA FINAL.

                                  Se comprobó que presenta datos
                                  inconsistentes en la mayoría
                                  de los registros.


X de Geometry SHP     --->        Longitud

                                  La longitud debe obtenerse
                                  directamente de la coordenada X
                                  de la geometría del punto.


Y de Geometry SHP     --->        Latitud

                                  La latitud debe obtenerse
                                  directamente de la coordenada Y
                                  de la geometría del punto.


Geometry SHP          --->        Geom

                                  Tipo destino:
                                      geometry

                                  SRID:
                                      4326

                                  La geometría original del SHP
                                  constituye la fuente espacial
                                  principal del registro.


-------------------------------------------------------------
CAMPOS UTILIZADOS SOLO DURANTE EL DIAGNOSTICO EN QGIS
-------------------------------------------------------------

geom_x

    Campo virtual creado mediante:

        x($geometry)

    Se utilizó únicamente para visualizar y comprobar
    la coordenada X de la geometría.

    NO forma parte del DBF original.
    NO se migra.


geom_y

    Campo virtual creado mediante:

        y($geometry)

    Se utilizó únicamente para visualizar y comprobar
    la coordenada Y de la geometría.

    NO forma parte del DBF original.
    NO se migra.


coord_key

    Campo virtual creado únicamente durante el diagnóstico.

    Se utilizó para agrupar registros que compartían
    exactamente la misma posición geométrica.

    Permitió detectar 10 pares de registros
    duplicados exactos.

    NO forma parte del DBF original.
    NO se migra.


-------------------------------------------------------------
ACLARACION PARA EL MIGRADOR
-------------------------------------------------------------

El migrador NO debe buscar campos llamados:

    geom_x
    geom_y
    coord_key

dentro del archivo DBF.

Estos campos fueron creados únicamente en QGIS para realizar
el diagnóstico.

Las coordenadas reales deben obtenerse directamente desde
la geometría almacenada en el archivo SHP:

    Geometry.X  ---> Longitud
    Geometry.Y  ---> Latitud

============================================================
*/

/*CAMPO GENERADO POR SQL SERVER:

    IdCodigo
        - No proviene del SHP.
        - Se genera mediante IDENTITY.
        - Será la clave primaria.

    Estado
        - No proviene del SHP.
        - Valor inicial por defecto: 1 = Normal.

    FechaCambioEstado
        - No proviene del SHP.
        - SQL Server asignará la fecha/hora automáticamente.


CAMPO DERIVADO ESPACIALMENTE:

    IdLote
        - No existe en el SHP de Código Fijo.
        - Se obtiene determinando qué lote contiene
          la geometría del punto.
        - Será FK hacia dbo.Lotes(IdLote).


REGLAS IMPORTANTES:

    1. Text no se migra porque contiene la misma información
       que CodF_SIG.

    2. Longi y Latid no deben utilizarse como fuente principal
       de coordenadas.

    3. Longitud y Latitud deben obtenerse desde Geom:
           Longitud = X de la geometría
           Latitud  = Y de la geometría

    4. CodFijo NO es único.
       Se detectaron:
           6.072 valores distintos
           189 valores de CodFijo repetidos.

    5. Se encontraron 10 pares de registros duplicados exactos.
       Los duplicados coinciden en atributos y geometría.

       Durante la migración:
           - conservar un único registro por pareja;
           - registrar el duplicado descartado como incidencia.

    6. CodF_SIG y CodF_SQL representan el mismo código
       en diferente formato.

    7. La geometría debe almacenarse con SRID 4326.

    8. IdLote se determinará después de tener cargada
       la capa de lotes mediante relación espacial.


REGISTROS ORIGINALES:
    6.271

DUPLICADOS EXACTOS REDUNDANTES:
    10

REGISTROS ÚNICOS ESPERADOS TRAS DEDUPLICACION:
    6.261

GEOMETRIAS VALIDAS:
    6.271

GEOMETRIAS INVALIDAS:
    0

============================================================
*/









--2) RESTRICCIONES



-- RESTRICCIONES DE LA CAPA CODIGOS FIJOS

/*
============================================================
RESTRICCIONES : dbo.CodigosFijos
============================================================

1. IdCodigo
   - PRIMARY KEY.
   - Se genera automáticamente mediante IDENTITY.
   - No proviene del SHP.

2. CodF_SQL
   - Permite NULL.
   - NO se define como UNIQUE.
   - Representa el mismo código que CodF_SIG,
     pero en formato numérico.

3. CodF_SIG
   - Permite NULL.
   - NO se define como UNIQUE.
   - Se conserva el valor original proveniente del SHP.

4. CodFijo
   - Permite NULL.
   - NO debe tener restricción UNIQUE.
   - En el diagnóstico se comprobaron valores repetidos.

5. Nombre
   - Permite NULL.
   - Se conserva directamente desde el SHP.

6. Estado
   - NOT NULL.
   - Valor inicial por defecto: 1.
   - Solo puede contener valores entre 1 y 5:

       1 = Normal
       2 = Para Corte
       3 = Cortado
       4 = Baja Parcial
       5 = Baja Total

7. FechaCambioEstado
   - NOT NULL.
   - SQL Server asignará automáticamente
     la fecha/hora inicial.
   - Se actualizará cuando cambie Estado.

8. IdLote
   - Permite NULL inicialmente.
   - Será FOREIGN KEY hacia dbo.Lotes(IdLote).
   - Su valor se obtiene mediante relación espacial
     entre el punto de Código Fijo y la geometría del lote.

9. Longitud
   - Permite NULL.
   - Se obtiene desde la coordenada X de la geometría.
   - NO debe obtenerse directamente desde Longi.

10. Latitud
    - Permite NULL.
    - Se obtiene desde la coordenada Y de la geometría.
    - NO debe obtenerse directamente desde Latid,
      debido a las inconsistencias detectadas.

11. Geom
    - Tipo geometry.
    - Debe almacenarse con SRID 4326.
    - En el SHP analizado:
          6.271 geometrías válidas
          0 geometrías inválidas.

12. DUPLICADOS
    - Se detectaron 10 pares de registros duplicados exactos.
    - El migrador deberá insertar un único registro
      por pareja y registrar el duplicado descartado.

============================================================
*/


-- 3) INDICES



-- INDICES DE LA CAPA CODIGOS FIJOS

/*
============================================================
INDICES : dbo.CodigosFijos
============================================================

1. Índice sobre CodFijo.
2. Índice sobre Nombre.
3. Índice sobre Estado.
4. Índice sobre IdLote.
5. Índice espacial sobre Geom.
============================================================
*/

CREATE INDEX IX_CodigosFijos_CodFijo
ON dbo.CodigosFijos(CodFijo);


CREATE INDEX IX_CodigosFijos_Nombre
ON dbo.CodigosFijos(Nombre);


CREATE INDEX IX_CodigosFijos_Estado
ON dbo.CodigosFijos(Estado);


CREATE INDEX IX_CodigosFijos_IdLote
ON dbo.CodigosFijos(IdLote);


CREATE SPATIAL INDEX SIX_CodigosFijos_Geom
ON dbo.CodigosFijos(Geom)
USING GEOMETRY_GRID
WITH
(
    BOUNDING_BOX = (-180, -90, 180, 90)
);


--FK CON LOTES


ALTER TABLE dbo.CodigosFijos
ADD CONSTRAINT FK_CodigosFijos_Lotes
FOREIGN KEY (IdLote)
REFERENCES dbo.Lotes(IdLote);


--CHECK DE ESTADO

ALTER TABLE dbo.CodigosFijos
ADD CONSTRAINT CK_CodigosFijos_Estado
CHECK (Estado BETWEEN 1 AND 5);