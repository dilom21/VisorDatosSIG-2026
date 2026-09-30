# FE-SIG 2 — Códigos Fijos reales en Leaflet

## Objetivo
Mostrar los **6261 registros reales** de `dbo.CodigosFijos` (SQL Server) en el
visor Web `/Visor`, consumiendo el GeoJSON protegido por JWT que expone la API y
representando cada punto con Leaflet.

Alcance estricto de esta fase: **sin mocks, sin JSON local, sin puntos inventados
y sin acceso directo a SQL desde JavaScript.** Los datos provienen únicamente de
la API real.

Flujo de datos implementado:

```
dbo.CodigosFijos.Geom  ->  CadastreRepository  ->  API protegida con JWT  ->  Web  ->  Leaflet
```

## Arquitectura
La fase se apoya en cuatro módulos JavaScript del visor, cada uno con una
responsabilidad única:

- `VisorSIG.geojson` (`geojson.js`): construye el *query string*, llama a la API
  a través de `VisorSIG.api.peticion` con el JWT de
  `VisorSIG.sesion.obtenerToken()` y `protegida: true`, y devuelve un resultado
  controlado. API pública: `obtenerExtensionCodigosFijos()` y
  `obtenerCodigosFijos(bounds, limit)`.
- `VisorSIG.markers` (`markers.js`): crea un `L.circleMarker` por cada *feature*
  de tipo `Point`, usando **un único renderer compartido** `L.canvas({ padding: 0.5 })`.
- `VisorSIG.capas` (`layers.js`): administra **una sola capa `L.geoJSON`
  reutilizable**, la carga por *bounding box*, el *debounce* y el control de
  activación de la capa.
- `VisorSIG.legend` (`legend.js`): leyenda de los estados de Códigos Fijos,
  construida una sola vez, con muestras de color tomadas de variables CSS.

Regla de diseño: **`geojson.js` no contiene lógica de Leaflet ni manipulación del
DOM**, y `markers.js` no decide el *bounding box* ni el ciclo de carga.

## Contrato consumido
Contrato verificado contra `origin/dev-alastor` en modo **solo lectura** (sin
*merge*, *cherry-pick* ni *checkout*):

| Endpoint | Respuesta esperada |
|---|---|
| `GET api/capas/extension?capa=codigosfijos` | 200 con `{ minX, minY, maxX, maxY, bbox }`; 404 cuando no hay geometría |
| `GET api/capas/codigosfijos/geojson?minX&minY&maxX&maxY&limit` | 200 con una `FeatureCollection` |

- Ambos endpoints **requieren JWT**.
- El parámetro `limit` se limita en el servidor al rango **1..10000**.
- Cada *feature* tiene la forma:
  - `id` = `IdCodigo`;
  - `geometry.type` = `"Point"` y `geometry.coordinates` = `[longitud, latitud]`;
  - `properties` = `CodF_SQL`, `CodF_SIG`, `CodFijo`, `Nombre`, `Estado`,
    `IdLote`, `Longitud`, `Latitud` (el diccionario del servidor no distingue
    mayúsculas y minúsculas).
- El posicionamiento usa **exclusivamente `feature.geometry.coordinates`**.
  `properties.Longitud` y `properties.Latitud` **no se usan** para ubicar el
  marcador. `L.geoJSON` convierte `[lng, lat]` a `[lat, lng]` de Leaflet.

## Sesión y JWT
- El token se obtiene de `VisorSIG.sesion.obtenerToken()` y se envía mediante
  `VisorSIG.api.peticion` con `protegida: true`.
- `guard.js` **no fue modificado**. `layers.js` espera a que desaparezca el
  estado `body.sesion-verificando` usando un `MutationObserver` idempotente que
  se desconecta a sí mismo (sin `setInterval`).
- Ambas funciones de `geojson.js` **siempre resuelven** (nunca rechazan) con un
  objeto de resultado controlado, de modo que un error de red o de autorización
  no rompe el visor.

## Bounding box y debounce
- La extensión **no está fija en el frontend**: se obtiene de la API mediante
  `obtenerExtensionCodigosFijos()`.
- La carga se hace por *bounding box* con el `limit` en 10000.
- El evento `moveend` se enlaza **exactamente una vez**, con un *debounce* de
  **250 ms**.
- Un contador interno `_secuenciaCarga` garantiza que una respuesta antigua no
  pueda reemplazar a una más nueva (protección contra condiciones de carrera).
- Antes de insertar datos nuevos se ejecuta `clearLayers()`, de modo que no se
  acumulan puntos entre movimientos.

## Renderizado con Canvas
- Se crea **un solo** `L.canvas({ padding: 0.5 })` compartido por todos los
  marcadores (verificado: exactamente **1 `<canvas>`** en el mapa).
- Se usa `L.circleMarker`, sin iconos DOM por punto ni un SVG pesado por punto.

## Datos reales en SQL y extensión obtenida
- SQL Server, tabla `dbo.CodigosFijos`: **6261 registros**,
  `GeometriasNull = 0`, `SridIncorrecto = 0`, SRID **4326**.
- Entorno de la prueba: API en `http://localhost:5080`, Web en
  `http://localhost:5000`.
- Extensión real devuelta por la API (200):

| Campo | Valor |
|---|---|
| minX | `-61.0038878963715` |
| minY | `-16.440702244308323` |
| maxX | `-60.928706512486414` |
| maxY | `-16.353002876274967` |

> **Nota de discrepancia:** el enunciado previo de la tarea indicaba
> `minY = -16.449702244308323`, pero la API devuelve realmente
> `minY = -16.440702244308323` (diferencia en el cuarto decimal). Se registra el
> valor real observado. La extensión no está escrita en el frontend: se obtiene
> de la API.

> **Nota SHP vs SQL:** el SHP original fue diagnosticado por el Migrador con
> **6271 registros**, mientras que SQL Server contiene actualmente **6261**. La
> causa de los 10 registros de diferencia **no se conoce** y **no se inventa**;
> queda pendiente contrastarla contra el reporte y la bitácora del Migrador en
> una tarea posterior.

## Representación
- `VisorSIG.markers` genera un `L.circleMarker` por cada `Point`, con un renderer
  Canvas único compartido.
- Helpers expuestos: `obtenerNombreEstado(estado)`, `obtenerColorEstado(estado)`,
  `crearPopup(feature)` y `crearMarker(feature, latlng)`.
- Los colores se leen de variables CSS mediante
  `getComputedStyle(document.documentElement)`: **no se escriben colores en
  hexadecimal dentro de JavaScript**.

## Estados
Traducción de `Estado` aplicada en el visor:

| Estado | Significado |
|---|---|
| 1 | Normal |
| 2 | Para Corte |
| 3 | Cortado |
| 4 | Baja Parcial |
| 5 | Baja Total |
| otro | Desconocido |

Variables CSS asociadas: `--estado-normal`, `--estado-para-corte`,
`--estado-cortado`, `--estado-baja-parcial`, `--estado-baja-total`,
`--estado-desconocido`.

## Popup seguro
- El popup se construye con `document.createElement` + `textContent` y se entrega
  a `bindPopup(node)`.
- **No** hay concatenación de cadenas con datos de la API ni uso de `innerHTML`.
- Los valores `NULL` o vacíos se muestran como `—`.
- Maquetación: la rejilla clave/valor se declara en `.visor-popup__lista` y cada
  `.visor-popup__fila` se disuelve con `display: contents`, de modo que los `dt` y
  `dd` entran directamente en esa rejilla y las claves y los valores quedan
  alineados entre todas las filas. La columna de claves usa `max-content` con
  `white-space: nowrap`, por lo que «Código fijo» o «ID interno» no se parten.
- Reverificación final **en el visor real, con datos reales** (no un DOM
  aislado): un clic sobre un punto del lienzo abrió el popup de la *feature*
  `id = 3` con seis filas — `Código SIG` 30.440.002, `Código fijo` 6585,
  `Nombre` TOMICHA LANDIVAR FELIPE, `Estado` Normal, `Lote` —, `ID interno` 3 —.
  Las seis filas coinciden una a una con `feature.properties` e `feature.id`, las
  claves y los valores quedan alineados entre filas, ninguna clave se corta, no
  hay *overflow*, el `NULL` de `Lote` se muestra como `—` y la consola quedó sin
  mensajes. Detalle en `P-FE-03`, sección G.

## Rendimiento
Decisiones adoptadas:
- `L.circleMarker` + Canvas.
- Carga por *bounding box* con `limit` 10000.
- *Debounce* de 250 ms.
- `clearLayers()` antes de cada inserción.
- Una sola capa reutilizable.
- Contador de secuencia contra condiciones de carrera.

Deliberadamente **no** utilizados: MarkerCluster, plugins externos, npm, CDN,
iconos DOM por punto, un SVG pesado por punto.

## Archivos modificados
6 archivos de código, ninguno más. Total: **947 líneas agregadas, 13 eliminadas**
(`git diff --numstat`).

| Archivo | Agregadas | Eliminadas |
|---|---|---|
| `src/VisorDatosSIG.Web/wwwroot/js/visor/geojson.js` | 168 | 1 |
| `src/VisorDatosSIG.Web/wwwroot/js/visor/layers.js` | 315 | 1 |
| `src/VisorDatosSIG.Web/wwwroot/js/visor/markers.js` | 172 | 1 |
| `src/VisorDatosSIG.Web/wwwroot/js/visor/legend.js` | 95 | 1 |
| `src/VisorDatosSIG.Web/Views/Visor/Index.cshtml` | 15 | 7 |
| `src/VisorDatosSIG.Web/wwwroot/css/visor.css` | 182 | 2 |

Los cuatro módulos `js/visor/*.js` eran *stubs* de una sola línea de comentario.

Detalle:
- `Index.cshtml`: Códigos Fijos pasa a ser una capa real con
  `<input type="checkbox" id="capa-codigos-fijos" checked>`, más
  `#estado-codigos-fijos`, `#conteo-codigos-fijos` y `#leyenda-codigos-fijos`.
  Manzanas / Lotes / Vías permanecen como `Pendiente`. La nota del panel ahora
  indica que Códigos Fijos ya consume información geográfica real.
- `visor.css`: se agregaron los tokens `--estado-*` y una sección
  "8. Capas del visor (FE-SIG 2)" con el control de capas, *badges*, conteo,
  leyenda, muestras de estado y estilos de popup, además de ajustes responsive
  dentro de las *media queries* existentes. La cabecera, Home y Login no se
  rediseñaron.

**No modificados:** todo lo que está bajo `src/VisorDatosSIG.Api`,
`src/VisorDatosSIG.Application`, `src/VisorDatosSIG.Infrastructure`,
`src/VisorDatosSIG.Domain`, `src/VisorDatosSIG.Migrador`, `database/`,
`js/auth/*.js`, `js/visor/map.js` y `js/visor/identify.js`.

## Prueba realizada
- `dotnet build VisorDatosSIG.sln` → **0 errores, 0 advertencias** (solo se
  detuvo y reinició `VisorDatosSIG.Web` para liberar la salida; el proceso de la
  API se dejó en ejecución).

**Verificación en navegador (carga inicial después del login):**
- *Badge* "Activa" y conteo visible "6261 puntos visibles".
- 6261 subcapas en la capa Leaflet, con 6261 `id` de *feature* únicos (sin
  duplicados).
- Exactamente 1 `<canvas>` en el mapa (renderer único compartido).
- Exactamente 1 petición GeoJSON, con *bbox*
  `minX=-61.04278564453126&minY=-16.477900905827223&maxX=-60.88949203491212&maxY=-16.31569129964241&limit=10000`
  (la vista ampliada por `fitBounds`, mayor que la extensión de la capa).
- Traza de red: `GET /api/autenticacion/me [200]`,
  `GET /api/capas/extension?capa=codigosfijos [200]`,
  `GET /api/capas/codigosfijos/geojson?...&limit=10000 [200]`.
- Consola del navegador: **sin mensajes** (ni errores ni advertencias).

**Popup de la primera *feature* (`id = 1`):**

| Campo | Valor |
|---|---|
| Código SIG | `30.445.002` |
| Código fijo | `5788` |
| Nombre | `AGUILAR SOCORE AGUSTIN EUGENIO` |
| Estado | `Normal` |
| Lote | `—` (NULL en la base de datos) |
| ID interno | `1` |
| geometry.type | `Point` |
| geometry.coordinates | `[-60.960764232925435, -16.38938968621091]` |

El `lat/lng` del marcador coincide exactamente con `geometry.coordinates`, lo que
confirma que la posición proviene de `geometry.coordinates`. Claves de
*properties* observadas: `CodF_SQL`, `CodF_SIG`, `CodFijo`, `Nombre`, `Estado`,
`IdLote`, `Longitud`, `Latitud`.

**Comportamiento por *bounding box*:**

| Escenario | Resultado |
|---|---|
| Zoom 17 | 313 puntos visibles, conteo igual a las *features* recibidas; *bbox* `minX=-60.96555948257447&minY=-16.394455999446517&maxX=-60.955978631973274&maxY=-16.3843175094094` |
| Zoom 15 | 5118 puntos visibles |
| 6 desplazamientos rápidos consecutivos | Solo 2 peticiones GeoJSON (*coalescing* por *debounce*); el estado final (3521 puntos visibles) coincide con el *bbox* final `minX=-60.96961498260498&minY=-16.409657598902164&maxX=-60.9312915802002&maxY=-16.36910363840734`; sin acumulación de duplicados |

Cada movimiento confirmado generó exactamente una petición por *bbox* resultante.

**Control de activación:**

| Acción | Resultado |
|---|---|
| Desactivar con el *checkbox* | Capa removida del mapa (`hasLayer` false), `estaActiva()` false, *badge* "Inactiva", conteo vacío, leyenda oculta; un desplazamiento posterior **no** generó petición GeoJSON (la lista de red no cambió) |
| Reactivar | Capa de nuevo en el mapa, leyenda visible de nuevo, recarga de 6261 para el *bbox* actual |

**Responsive (emulación de *viewport*, mediciones reales):**

| Ancho | Resultado |
|---|---|
| 1366 | Sin desbordamiento horizontal (`scrollWidth` 1366 = `innerWidth`); dos columnas "320px 896px" (panel 320, mapa 896x608); *checkbox* dentro del *viewport*; leyenda a una columna |
| 768 | Sin desbordamiento horizontal (768 = 768); una columna de 736px; mapa 736x440; *checkbox* y *badge* dentro del *viewport*; leyenda a 2 columnas |
| 360 | Sin desbordamiento horizontal (`scrollWidth` de `document` y `body` = 360); una columna; mapa 328x420; *checkbox* 16x16 dentro del *viewport*; *badge* dentro del *viewport*; leyenda a 2 columnas; los ítems del panel se envuelven |

**JWT / sesión:**

| Verificación | Resultado |
|---|---|
| `GET /api/capas/extension?capa=codigosfijos` sin token | 401 |
| `GET /api/capas/codigosfijos/geojson?...` sin token | 401 |
| Extensión con token inválido `Bearer token.invalido.xyz` | 401 |
| Logout | Navegación a `/Cuenta/Login?motivo=sesion-finalizada`; entrada `visorSIG.sesion` de `sessionStorage` limpiada; bloque de cabecera anónimo visible; formulario de login presente |
| `/Visor` sin sesión | Redirección a `/Cuenta/Login?returnUrl=%2FVisor` |

Queda registrada como caso `P-FE-03` en `docs/10_Pruebas/` (APROBADO).

## Resultado
- Consumo real del GeoJSON protegido por JWT y representación de los 6261
  registros en Leaflet: **verificado**.
- Extensión obtenida de la API (no fija en el frontend): **verificado**.
- Renderer Canvas único y ausencia de duplicados en la carga inicial:
  **verificado**.
- `moveend` con *debounce* de 250 ms y protección de secuencia: **verificado**.
- Popup seguro con datos reales y `NULL` como `—`: **verificado**.
- Popup reverificado en el visor real con clic sobre un punto, tras la corrección
  de maquetación (seis filas alineadas, sin claves cortadas, sin *overflow*):
  **verificado**.
- Control de activación de la capa: **verificado**.
- Build limpio: **0 errores, 0 advertencias**.
- Caso de prueba `P-FE-03`: **APROBADO**.

## Limitaciones
- **Solo** está implementada la capa de Códigos Fijos. Manzanas, Lotes y Vías
  siguen pendientes.
- `identify.js` continúa siendo un *placeholder*: `/api/capas/identificar`
  **no** se consume en esta fase; el popup del punto cubre el alcance de FE-SIG 2.
- La respuesta GeoJSON está limitada por el `limit` 10000; por encima de ese
  valor el servidor trunca y el frontend **no** pagina (con 6261 registros el
  límite no se alcanza hoy).
- No hay identificación espacial, ni filtros por atributos, ni exportación.
- Las capturas de evidencia se tomaron pero se mantuvieron **fuera del
  repositorio**; no se creó un registro de evidencias para esta fase.
- La diferencia entre los 6271 registros del SHP y los 6261 de SQL Server queda
  pendiente de contraste con el reporte y la bitácora del Migrador.
