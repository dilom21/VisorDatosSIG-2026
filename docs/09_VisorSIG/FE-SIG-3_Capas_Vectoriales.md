# FE-SIG 3 — Capas vectoriales reales en el visor

## Objetivo
Extender el visor Web `/Visor` para mostrar **cuatro capas SIG reales** de SQL
Server a través de la API protegida por JWT: Manzanas
(`Polygon`/`MultiPolygon`), Lotes (`Polygon`/`MultiPolygon`), Códigos Fijos
(`Point`, ya resuelto en FE-SIG 2) y Vías (`LineString`/`MultiLineString`).

Alcance estricto de esta fase: **sin mocks, sin JSON local y sin acceso a SQL
desde JavaScript.** Todos los datos provienen de la API real.

Flujo de datos implementado:

```
SQL Server  ->  API GeoJSON protegida con JWT  ->  VisorSIG.api.peticion  ->  L.geoJSON  ->  Leaflet
```

## Conteos reales
`GET api/capas` (catálogo, 200) devolvió exactamente:

| Capa | Descripcion | TipoGeometria | TotalRegistros |
|---|---|---|---|
| Manzanas | Delimitación de manzanas | Polygon | 863 |
| Lotes | Parcelas o lotes | Polygon | 15280 |
| CodigosFijos | Puntos de códigos fijos | Point | 6261 |
| Vias | Ejes viales | LineString | 578 |

`GET api/capas/extension` **sin parámetro de capa** devolvió la extensión
**global** (200) sobre las cuatro capas:

| Campo | Valor |
|---|---|
| minX | `-61.0077113160026` |
| minY | `-16.440702244308323` |
| maxX | `-60.91940886095809` |
| maxY | `-16.32146700608952` |

La extensión **no está fija en el frontend**: se lee de la API.

Extensión inicial enviada en la primera consulta GeoJSON (vista ampliada por
`fitBounds`):

```
minX=-61.04021072387696&minY=-16.462262219456488&maxX=-60.88691711425782&maxY=-16.300039585864067&limit=10000
```

Conteos visibles en esa extensión inicial: Manzanas **863**, Códigos Fijos
**6.261**, Vías **578**; Lotes alcanzó el límite (ver sección «Límite 10000»).

### Catálogo frente a visible
El visor distingue dos números por capa:

- **Total almacenado** (columna `TotalRegistros` del catálogo).
- **Visible** (features realmente recibidas para el *bbox* actual).

Los totales del catálogo se usan **solo** para mostrar la diferencia entre lo
visible y lo almacenado. Un fallo del catálogo **no** rompe el mapa: en ese caso
los totales simplemente se omiten.

> **Nota de artefacto:** el diagnóstico previo (época FE-SIG 2) mencionaba
> «Manzanas 863» como ejemplo. El total real del catálogo medido ahora es 863
> para Manzanas, 15280 para Lotes, 6261 para Códigos Fijos y 578 para Vías. Se
> registran los valores reales observados; no se inventan ni se redondean.

## Contrato consumido
Contratos verificados contra `origin/dev-alastor` en modo **solo lectura** (sin
*merge*, *cherry-pick* ni *checkout*):

| Endpoint | Respuesta esperada |
|---|---|
| `GET api/capas` | 200 con un arreglo de `{ Capa, Descripcion, TipoGeometria, TotalRegistros }`; `Capa` ∈ { `Manzanas`, `Lotes`, `CodigosFijos`, `Vias` } |
| `GET api/capas/extension` (sin parámetro) | 200 con `{ minX, minY, maxX, maxY, bbox }` = extensión global sobre las cuatro capas |
| `GET api/capas/extension?capa=<x>` | Extensión por capa |
| `GET api/capas/<capa>/geojson?minX&minY&maxX&maxY&limit` | 200 con una `FeatureCollection` |

- **Todos requieren JWT** (`[Authorize]`).
- El parámetro `limit` se limita en el servidor al rango **1..10000**.
- Contratos de *feature* (el diccionario de *properties* del servidor no
  distingue mayúsculas y minúsculas):
  - `manzanas`: `id` = `IdManzana`; properties `IdOrigen`, `UV_MZA`, `UV`, `MZA`.
  - `lotes`: `id` = `IdLote`; properties `IdOrigen`, `NroLote`, `IdManzana`.
  - `codigosfijos`: **sin cambios respecto de FE-SIG 2** — `id` = `IdCodigo`;
    properties `CodF_SQL`, `CodF_SIG`, `CodFijo`, `Nombre`, `Estado`, `IdLote`,
    `Longitud`, `Latitud`; `Point` en `[lng, lat]`.
  - `vias`: `id` = `IdVia`; properties `OBJECTID`, `Nombre`, `TipoVia`, `OSMID`.
- La geometría y la forma provienen **siempre** de `feature.geometry`; **nunca**
  se reconstruyen a partir de las *properties*.

## Arquitectura
### `VisorSIG.geojson` (`geojson.js`) — generalizado
Módulo sin lógica de Leaflet ni manipulación del DOM. API pública:

| Función | Retorno |
|---|---|
| `obtenerCatalogo()` | `{ ok, status, error, catalogo }` |
| `obtenerExtension(capa)` (capa opcional; sin argumento = global) | `{ ok, status, error, extension }` |
| `obtenerCapa(capa, bounds, limit)` | `{ ok, status, error, coleccion }` |

- Se conservan *wrappers* de compatibilidad: `obtenerExtensionCodigosFijos()` y
  `obtenerCodigosFijos(bounds, limit)`.
- **Siempre resuelve** (nunca rechaza) y valida mínimamente la `FeatureCollection`.
- Usa `VisorSIG.api.peticion` con `protegida: true` y el JWT de
  `VisorSIG.sesion.obtenerToken()`. **No hay `fetch()` en ninguna parte.**

### `VisorSIG.features` (`features.js`, archivo nuevo)
Simbología y popups seguros de Manzanas, Lotes y Vías, más **un renderer Canvas
reutilizable por capa vectorial**. API pública: `obtenerRendererManzanas()`,
`obtenerRendererLotes()`, `obtenerRendererVias()`, `estiloManzana`,
`estiloLote`, `estiloVia`, `crearPopupManzana`, `crearPopupLote`, `crearPopupVia`.

### `VisorSIG.capas` (`layers.js`) — registro
Refactorizado a **un único registro `CAPAS`** con las cuatro definiciones
(`clave`, `etiqueta`, `api`, `idCheckbox`, `idEstado`, `idConteo`, `limite`
10000, `activaInicial`, `catalogo`, `tipo`). **No** hay cuatro bloques
duplicados.

- El estado por capa vive en un único objeto `ns._estadoCapas` (reutilizado si
  el script se carga dos veces).
- Cada capa crea **un solo** `L.geoJSON` reutilizable, con su propio *bbox*, su
  propio contador `secuencia` contra condiciones de carrera, estados de
  carga/error/vacío y control de activación. **No** se hace ninguna petición
  mientras la capa está inactiva.
- **Un único** *listener* `moveend` para las cuatro capas, con **un solo**
  *debounce* de **250 ms**, que llama a `cargarActivas()`, la cual itera
  únicamente las capas activas. En el archivo hay **exactamente un
  `on('moveend')`**.
- API pública: `inicializar`, `activar(clave)`, `desactivar(clave)`,
  `estaActiva(clave)`, `obtenerCapa(clave)`, `cargarTodas()`.

### `VisorSIG.legend` (`legend.js`)
Leyenda con un bloque «Capas activas» (Manzanas, Lotes, Vías) y el bloque
existente «Estados de Código Fijo». `actualizar(activas)` solo alterna `hidden`
sobre los nodos existentes (**nunca** reconstruye el DOM) y oculta el bloque de
estados cuando Códigos Fijos está inactiva.

## Sesión y JWT
- El token se obtiene de `VisorSIG.sesion.obtenerToken()` y se envía mediante
  `VisorSIG.api.peticion` con `protegida: true`.
- `guard.js` **no fue modificado**.
- `geojson.js` **siempre resuelve** con un objeto de resultado controlado, de
  modo que un error de red o de autorización no rompe el visor.

## Bounding box y debounce único
- La carga inicial usa la **extensión global** (`obtenerExtension()` sin
  argumento) y ajusta la vista con
  `fitBounds(bounds, { padding: [20, 20], animate: false })`.
- Después del ajuste, vía `requestAnimationFrame` se ejecuta `cargarActivas()` y
  **solo entonces** se enlaza `moveend`, de modo que el `moveend` del
  `fitBounds` no duplica la petición.
- Cada capa mantiene su propio *bbox* y su propio contador `secuencia`, que
  descarta respuestas obsoletas. La desactivación también incrementa la
  secuencia, de modo que una respuesta en vuelo no puede repintar el panel.
- `clearLayers()` se ejecuta **antes** de cada `addData`, de modo que no se
  acumulan geometrías entre movimientos.
- Si la extensión global falla, el mapa base conserva su centro inicial, cada
  capa muestra un error controlado y el mapa sigue funcionando.

## Catálogo
- `GET api/capas` alimenta los totales de las cuatro capas.
- Los totales del catálogo se usan solo para contrastar «visible» contra
  «almacenado».
- Un fallo del catálogo no rompe el mapa: los totales se omiten.

## Simbología
Estilos definidos en `features.js`:

| Capa | weight | opacity | fillOpacity | fill |
|---|---|---|---|---|
| Manzanas | 1.5 | 0.85 | 0.12 | Sí |
| Lotes | 1 | 0.7 | 0.06 | Sí |
| Vías | 2.5 | 0.9 | — | No (`fill: false`) |

Los colores se leen **solo** de variables CSS (no hay hexadecimal en
JavaScript): `--capa-manzanas-stroke`, `--capa-manzanas-fill`,
`--capa-lotes-stroke`, `--capa-lotes-fill`, `--capa-vias-stroke`.

## Renderizado con Canvas
- Un renderer Canvas reutilizable **por capa vectorial**, provisto por
  `features.js`.
- Códigos Fijos conserva su `L.circleMarker` + renderer Canvas compartido de
  FE-SIG 2, **sin cambios**.
- No se usan iconos DOM por punto ni un SVG pesado por elemento.

## Z-order
Paneles personalizados con `zIndex` explícito:

| Panel | zIndex |
|---|---|
| `paneManzanas` | 350 |
| `paneLotes` | 360 |
| `paneVias` | 370 |
| `overlayPane` (Códigos Fijos, por defecto) | 400 |
| *popup pane* | 700 |

Medido: `ordenVisualCorrecto` **true**. Los controles de Leaflet y los *tiles*
no se ven afectados.

## Activación y estados iniciales
Estados iniciales:

| Capa | Estado inicial |
|---|---|
| Manzanas | **Activa** |
| Lotes | **Inactiva** (es la capa más pesada) |
| Códigos Fijos | **Activa** |
| Vías | **Activa** |

En la carga inicial se emitió **exactamente una petición por capa activa**
(`manzanas`, `codigosfijos`, `vias`). **No** se emitió ninguna petición para
`lotes`, que arranca deshabilitada.

Al desactivar con el *checkbox*: la capa se remueve del mapa (`hasLayer`
false), el *badge* pasa a `Inactiva`, el conteo queda vacío y su entrada
desaparece de la leyenda. Un desplazamiento posterior **no** genera petición
GeoJSON para esa capa.

## Límite 10000
El límite aplica a cualquier capa que lo alcance. Para Lotes (15280
almacenados) el cargador devuelve **exactamente 10000** *features* y el panel
muestra:

- *Badge* `Aviso` (clase `visor-badge--aviso`).
- Texto de conteo:
  `10.000 cargados — límite alcanzado; acerque el mapa · 15.280 en total`.

La interfaz **nunca** afirma que haya más elementos que el límite: solo indica
que se alcanzó el límite. La región viva consolidada agrega:
`Lotes: se alcanzó el límite de 10.000 elementos cargados.`

## Popups
Construidos con `document.createElement` + `textContent` (**nunca**
`innerHTML`). Los valores `NULL` o vacíos se muestran como `—`. El popup se
enlaza como **función**, de modo que Leaflet construye el DOM solo cuando el
popup se abre (miles de geometrías no crean miles de nodos por adelantado).

Todos los popups fueron abiertos con **datos reales**, haciendo clic sobre la
geometría. Cada fila se comparó una a una contra la *feature* real de la API;
todas coincidieron.

| Capa / feature | Filas verificadas |
|---|---|
| Manzana (MultiPolygon, `id` 1) | UV_MZA —, UV —, MZA —, IdOrigen —, ID interno 1 |
| Lote (MultiPolygon, `id` 261) | NroLote L07, IdManzana —, IdOrigen —, ID interno 261 |
| Vía (MultiLineString, `id` 103) | Nombre —, TipoVia residential, OSMID 160459911, OBJECTID 103, ID interno 103 |
| Código Fijo (Point, `id` 911) | Código SIG 16.200.025, Código fijo 6257, Nombre LIMACHI CASUPA MARIA FERNANDA, Estado Normal, Lote —, ID interno 911 |

Maquetación verificada en los cuatro: claves y valores alineados, ninguna clave
partida, sin *overflow* horizontal, popup dentro del *viewport* y el mismo
estilo visual que el popup aprobado en FE-SIG 2.

## Hallazgo de arquitectura: clic en el Canvas
Leaflet enlaza el manejador de clic del renderer Canvas a **su propio elemento
`<canvas>`** y marca ese canvas con `_leaflet_disable_events`, de modo que el
mapa ignora los eventos originados en él. Con un canvas por capa, el canvas del
panel más alto (Códigos Fijos, `overlayPane` por defecto, `zIndex` 400) recibía
todos los clics y las capas inferiores **nunca** podían abrir su popup
(verificado: sobre una Manzana el clic llegaba al mapa pero nunca a la capa).

Resolución (solo dentro de los archivos autorizados, `layers.js`): los cuatro
renderers Canvas reciben `pointer-events: none`, y **un único despachador de
clic a nivel de mapa** (`mapa.on('click', despacharClic)`) hace *hit-test* sobre
las capas con el propio `layer._containsPoint(evento.layerPoint)` de Leaflet, en
el orden Códigos Fijos -> Vías -> Lotes -> Manzanas (de arriba hacia abajo), y
abre el popup del primer acierto mediante `capa.openPopup(evento.latlng)`.

Consecuencia: las cuatro capas conservan su propio popup **y** el orden visual
solicitado, y los puntos siguen siendo clicables por encima de los polígonos.
`markers.js` **no** fue modificado.

### Corrección de cierre: el puntero de los renderers se desactivaba demasiado pronto

En la re-ejecución de cierre se detectó que, al **activar Lotes** después del
arranque, la prioridad no se cumplía: un clic sobre un Código Fijo abría el
popup del Lote. Causa: el `<canvas>` de un renderer se crea en su **primer
render**, no al construir el renderer; `desactivarPunteroDeRenderers()` se
ejecutaba al activar la capa, **antes** de ese primer render, por lo que el
`<canvas>` de Lotes quedaba interactivo. Leaflet marca ese canvas con
`_leaflet_disable_events`, el mapa ignora el evento y **el despachador nunca se
ejecutaba**; el popup lo abría el manejador nativo del canvas.

Corrección (mínima, en `layers.js`): reaplicar `desactivarPunteroDeRenderers()`
**después** de `addData` (dentro de `aplicar`), cuando el `<canvas>` ya existe.
El despachador y el orden de prioridad **no** se reescribieron.

Verificado tras la corrección: los cuatro `<canvas>` quedan con
`pointer-events: none`; el despachador se ejecuta en cada clic y el orden
**Códigos Fijos -> Vías -> Lotes -> Manzanas** se cumple, abriendo siempre un
único popup. Detalle en `P-FE-04`, sección **L**.

## Deuda técnica (no bloqueante)
- **`layer._containsPoint(...)` (Leaflet 1.9.4):** el despachador hace el
  *hit-test* con un método **interno** del renderer Canvas de Leaflet 1.9.4, que
  está **fijado localmente** en el repositorio (`wwwroot/lib/leaflet`). Se usa
  porque reproduce exactamente el hit-test del propio renderer y el acceso queda
  **encapsulado** en `buscarObjetivoEnPunto` (`layers.js`). Antes de invocarlo ya
  existe la defensa `typeof layer._containsPoint === 'function'`, que evita un
  `TypeError` si el método desapareciera. **Debe revisarse al actualizar
  Leaflet.** No bloquea FE-SIG 3.
- **Coordinación con FE-SIG 4:** la identificación espacial (FE-SIG 4) deberá
  coordinar su propio manejo de `map.click` con este despachador único para no
  generar **identificaciones duplicadas**.
- **`/favicon.ico` (404):** queda **fuera** de FE-SIG 3. Se registra como
  pendiente **menor**; no se creó favicon ni se modificó `Layout`.

## Rendimiento
Decisiones adoptadas:
- Canvas por capa (sin nodos DOM por geometría).
- Carga por *bounding box* con `limit` 10000.
- Una capa `L.geoJSON` reutilizable por capa.
- `clearLayers()` antes de cada inserción.
- **Un solo** *listener* `moveend` para las cuatro capas.
- *Debounce* de 250 ms.
- Contador `secuencia` por capa contra condiciones de carrera.
- Lotes inicia **inactiva** (la capa más pesada) para no cargar 10000 polígonos
  innecesariamente al abrir.

Deliberadamente **no** utilizados: MarkerCluster, plugins externos, npm, CDN,
iconos DOM por punto, un SVG pesado por elemento.

## Archivos modificados
5 archivos modificados y 1 creado. `git diff --stat` de los 5 modificados:
**923 inserciones (+), 152 eliminaciones (-)**.

| Archivo | Cambio |
|---|---|
| `src/VisorDatosSIG.Web/wwwroot/js/visor/features.js` | **Creado** |
| `src/VisorDatosSIG.Web/Views/Visor/Index.cshtml` | 46 |
| `src/VisorDatosSIG.Web/wwwroot/css/visor.css` | 83 |
| `src/VisorDatosSIG.Web/wwwroot/js/visor/geojson.js` | 104 |
| `src/VisorDatosSIG.Web/wwwroot/js/visor/layers.js` | 664 |
| `src/VisorDatosSIG.Web/wwwroot/js/visor/legend.js` | 178 |

Las cifras de la tabla cuentan adiciones y eliminaciones juntas.

Detalle:
- `Index.cshtml`: cuatro *checkboxes* reales (`capa-manzanas` marcado,
  `capa-lotes` **no** marcado, `capa-codigos-fijos` marcado, `capa-vias`
  marcado), cuatro *badges* (`estado-*`), cuatro conteos (`conteo-*`, sin
  `aria-live` para no crear cuatro regiones vivas que anuncien en cada
  desplazamiento), `#leyenda-capas` y **una** región viva consolidada
  `#resumen-capas` con `role="status" aria-live="polite"`. Orden de scripts:
  leaflet, map, geojson, features, markers, layers, legend, identify. No quedan
  etiquetas `Pendiente`.
- `visor.css`: sección renombrada «8. Capas del visor (FE-SIG 2 y 3)»; se
  agregaron los tokens de FE-SIG 3, `.visor-badge--aviso`,
  `.visor-panel__conteos`, `.visor-resumen`, las muestras de leyenda por tipo
  (`.visor-leyenda__swatch--poligono`, `--linea`), `.visor-popup__titulo` y
  ajustes responsive. Cabecera, Home y Login no se rediseñaron.
- Leyenda: `#leyenda-capas` con un bloque «Capas activas» (Manzanas, Lotes,
  Vías) y el bloque existente «Estados de Código Fijo».

**No modificados:** todo lo que está bajo `Api`, `Application`,
`Infrastructure`, `Domain`, `Migrador`, `database/`, `auth/*.js`, `map.js`,
`markers.js` e `identify.js`.

## Prueba realizada
- `dotnet build VisorDatosSIG.sln` -> **0 errores, 0 advertencias** (solo se
  detuvo y reinició `VisorDatosSIG.Web` para liberar la salida; el proceso de la
  API se dejó en ejecución).
- `git diff --check` limpio.
- Verificación en navegador, JWT, límite, activación, popups, rendimiento,
  regresión y responsive: detalle completo en el caso **`P-FE-04`**
  (`docs/10_Pruebas/P-FE-04_Capas_Vectoriales.md`).
- Consola: el **único** mensaje es un 404 de
  `http://localhost:5000/favicon.ico`, un archivo estático ausente sin relación
  con FE-SIG 3. Se registra honestamente: la consola **no** está literalmente
  vacía por ese 404 preexistente.

## Resultado
- Consumo real de las cuatro capas desde la API protegida por JWT, sin mocks:
  **verificado**.
- Conteos reales del catálogo y extensión global leída de la API (no fija en el
  frontend): **verificado**.
- Una petición por capa activa en la carga inicial; Lotes inactiva sin
  petición: **verificado**.
- Capas inactivas nunca consultadas: **verificado**.
- Límite 10000 en Lotes con aviso explícito: **verificado**.
- Cuatro popups con datos reales y `NULL` como `—`: **verificado**.
- Orden visual correcto y clic resuelto con despachador único:
  **verificado**.
- Regresión FE-SIG 2 (Códigos Fijos, `markers.js` intacto): **verificado**.
- Responsive 1366 / 768 / 360: **verificado**.
- Caso de prueba `P-FE-04`: **APROBADO**.

## Limitaciones
- Solo están implementadas las **cuatro** capas indicadas (Manzanas, Lotes,
  Códigos Fijos y Vías).
- `identify.js` continúa siendo un *placeholder*: `/api/capas/identificar`
  **no** se consume en esta fase. La identificación espacial general queda para
  FE-SIG 4.
- No hay paginación más allá del `limit` 10000: por encima de ese valor el
  servidor trunca y el frontend no pagina (Lotes queda hoy en 10000 de 15280).
- No hay filtros por atributos ni exportación.
- Las capturas de evidencia se tomaron pero se conservaron **fuera del
  repositorio**; no se creó un registro de evidencias para esta fase.
