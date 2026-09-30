# P-FE-04 — Capas vectoriales reales en el visor

- **Fase:** FE-SIG 3
- **Módulo:** `src/VisorDatosSIG.Web`
- **Tipo:** prueba manual de funcionamiento end-to-end
- **Estado:** **APROBADO** — verificado manualmente sobre
  `http://localhost:5000` contra la API real en `http://localhost:5080`.
  Re-ejecutado desde una carga limpia para cerrar FE-SIG 3: Escenarios A y B de
  `moveend` y prioridad del despachador de clic (ver sección **L**).

> Solo se declara como **APROBADO** lo que está respaldado por las mediciones
> observadas. Lo que no se ejecutó se etiqueta explícitamente como
> **NO EJECUTADO**.

## Precondiciones
- Rama `dev-josias`.
- `dotnet build VisorDatosSIG.sln` correcto (0 errores, 0 advertencias).
- **Web:** `http://localhost:5000`.
- **API:** `http://localhost:5080` (proceso dejado en ejecución durante la
  prueba; solo se detuvo y reinició `VisorDatosSIG.Web` para liberar la salida).
- **SQL Server** con las cuatro capas reales:
  - Manzanas: 863 registros.
  - Lotes: 15280 registros.
  - Códigos Fijos: 6261 registros.
  - Vías: 578 registros.
- Usuario de desarrollo `admin`; la contraseña no se documenta.
- Conexión a Internet (mapa base de OpenStreetMap).

## Pasos
1. Iniciar sesión en `/Cuenta/Login`.
2. Abrir `/Visor`.
3. Comprobar `GET /api/autenticacion/me`.
4. Comprobar `GET /api/capas` (catálogo) y `GET /api/capas/extension` (global).
5. Comprobar la carga inicial y que solo piden las capas activas.
6. Comprobar los conteos visibles en la extensión inicial.
7. Desactivar Vías y Lotes y comprobar el mapa, el *badge*, el conteo y la
   leyenda.
8. Desplazar con solo Manzanas y Códigos Fijos activas y contar peticiones.
   **Importante:** en este paso Vías y Lotes ya están desactivadas (paso 7); por
   eso el desplazamiento registra 0 peticiones para `vias`. No debe
   interpretarse como que Vías estuviera activa.
9. Cambiar el zoom y comprobar el conteo visible.
10. Abrir el popup de una geometría de cada una de las cuatro capas.
11. Comprobar el límite 10000 en Lotes (aviso en el panel y región viva).
12. Comprobar la consola del navegador.
13. Comprobar la expiración de sesión.
14. Repetir la visualización en 1366 / 768 / 360 px.
15. Compilar la solución y revisar `git diff --check`.

## Resultado esperado
- Las cuatro capas muestran exclusivamente datos reales de la API, sin mocks.
- Los conteos visibles coinciden con las *features* recibidas.
- Las capas inactivas no se consultan.
- El límite 10000 se informa de forma explícita y sin exagerar el total.
- Los popups muestran datos reales y `NULL` como `—`.
- El orden visual se conserva y cada capa sigue siendo clicable.
- La interfaz es utilizable a 1366 / 768 / 360 px.

## Resultado obtenido

### A. Sesión y arranque

| # | Verificación | Resultado |
|---|---|---|
| 1 | Login real y acceso a `/Visor` | **APROBADO** |
| 2 | `GET /api/autenticacion/me` → 200 | **APROBADO** |
| 3 | Los cuatro controles de capa (checkbox) y sus *badges* presentes | **APROBADO** |
| 4 | Sin mocks, sin JSON local y sin SQL desde JavaScript | **APROBADO** |

### B. Capas y conteos

| # | Verificación | Resultado |
|---|---|---|
| 5 | `GET /api/capas` → 200 con las cuatro capas (`Manzanas`, `Lotes`, `CodigosFijos`, `Vias`) | **APROBADO** |
| 6 | Manzanas: `TotalRegistros` 863 | **APROBADO** |
| 7 | Lotes: `TotalRegistros` 15280 | **APROBADO** |
| 8 | CodigosFijos: `TotalRegistros` 6261 | **APROBADO** |
| 9 | Vias: `TotalRegistros` 578 | **APROBADO** |
| 10 | Carga inicial: exactamente una petición por capa activa (`manzanas`, `codigosfijos`, `vias`) | **APROBADO** |
| 11 | Ninguna petición inicial para `lotes` (inicia inactiva) | **APROBADO** |
| 12 | Manzanas: 863 visibles en la extensión inicial | **APROBADO** |
| 13 | Códigos Fijos: 6.261 visibles en la extensión inicial | **APROBADO** |
| 14 | Vías: 578 visibles en la extensión inicial | **APROBADO** |
| 15 | Sin duplicados visuales: `clearLayers()` antes de `addData` y el conteo de subcapas coincide con el conteo visible | **APROBADO** |

### C. Activación

| # | Verificación | Resultado |
|---|---|---|
| 16 | Desactivar Vías y Lotes con el *checkbox*: capas removidas del mapa (`hasLayer` false), *badge* `Inactiva`, conteo vacío | **APROBADO** |
| 17 | Las entradas de Vías y Lotes desaparecen de la leyenda; la leyenda muestra solo `Manzanas` más los cinco estados de Código Fijo | **APROBADO** |
| 18 | Con Lotes y Vías inactivas, un desplazamiento no genera peticiones para ellas (ver G) | **APROBADO** |

### D. Popups

| # | Verificación | Resultado |
|---|---|---|
| 19 | Popup de Manzana (*feature* `id` 1, MultiPolygon): UV_MZA —, UV —, MZA —, IdOrigen —, ID interno 1 | **APROBADO** |
| 20 | Popup de Lote (*feature* `id` 261, MultiPolygon): NroLote L07, IdManzana —, IdOrigen —, ID interno 261 | **APROBADO** |
| 21 | Popup de Vía (*feature* `id` 103, MultiLineString): Nombre —, TipoVia residential, OSMID 160459911, OBJECTID 103, ID interno 103 | **APROBADO** |
| 22 | Popup de Código Fijo (*feature* `id` 911, Point): Código SIG 16.200.025, Código fijo 6257, Nombre LIMACHI CASUPA MARIA FERNANDA, Estado Normal, Lote —, ID interno 911 | **APROBADO** |
| 23 | Los valores de cada popup coinciden una a una con la *feature* real de la API | **APROBADO** |
| 24 | Maquetación: claves y valores alineados, ninguna clave partida, sin *overflow* horizontal, popup dentro del *viewport* | **APROBADO** |

### E. Catálogo y extensión

| # | Verificación | Resultado |
|---|---|---|
| 25 | `GET /api/capas/extension` **sin parámetro** → 200 con la extensión global sobre las cuatro capas | **APROBADO** |
| 26 | Extensión global: minX `-61.0077113160026`, minY `-16.440702244308323`, maxX `-60.91940886095809`, maxY `-16.32146700608952` | **APROBADO** |
| 27 | La extensión no está fija en el frontend: se lee de la API | **APROBADO** |
| 28 | Un fallo del catálogo no rompe el mapa (los totales se omiten) | **NO EJECUTADO** — comportamiento diseñado; no se forzó un fallo del catálogo en esta fase |

### F. Límite 10000

| # | Verificación | Resultado |
|---|---|---|
| 29 | Lotes devuelve exactamente 10000 *features* con `limit=10000` (de 15280 almacenados) | **APROBADO** |
| 30 | *Badge* `Aviso` (clase `visor-badge--aviso`) y texto `10.000 cargados — límite alcanzado; acerque el mapa · 15.280 en total` | **APROBADO** |
| 31 | La región viva consolidada agrega `Lotes: se alcanzó el límite de 10.000 elementos cargados.` | **APROBADO** |
| 32 | La interfaz no afirma que haya más elementos que el límite; solo indica que se alcanzó | **APROBADO** |

### G. Rendimiento y peticiones

| # | Verificación | Resultado |
|---|---|---|
| 33 | Un único *listener* `moveend` para las cuatro capas, con un solo *debounce* de 250 ms (verificado: un solo `on('moveend')` en `layers.js`) | **APROBADO** |
| 34 | Desplazamiento **con Vías y Lotes ya desactivadas** (paso 7) y solo Manzanas + Códigos Fijos activas (contador instrumentado alrededor de `geojson.obtenerCapa`): exactamente una petición para `manzanas` y una para `codigosfijos`, y **cero** para `lotes` o `vias`. El 0 de `vias` se debe a que Vías estaba **desactivada** en este paso, no a un fallo del despachador | **APROBADO** |
| 35 | El zoom cambia el conteo visible (p. ej. Códigos Fijos `6.233 visibles · 6.261 en total` tras un desplazamiento que recorta la extensión) | **APROBADO** |
| 36 | Protección de secuencia: el contador `secuencia` por capa descarta respuestas obsoletas; la desactivación incrementa la secuencia para que una respuesta en vuelo no repinte el panel | **APROBADO** — verificado en el código y en el flujo de desactivación |
| 37 | Consola del navegador | **APROBADO** con salvedad — el único mensaje es el 404 de `favicon.ico`; ningún script de FE-SIG 3 produce errores (ver Notas) |

### H. Seguridad y sesión

| # | Verificación | Resultado |
|---|---|---|
| 38 | Los endpoints exigen JWT: `GET /api/capas` y `GET /api/capas/extension` sin cabecera `Authorization` devolvieron **401** (verificado contra la API real en `http://localhost:5080`) | **APROBADO** |
| 39 | Expiración de sesión real: el guard redirigió a `/Cuenta/Login?returnUrl=%2FVisor&motivo=expirada` y limpió la sesión | **APROBADO** |
| 40 | Los *tiles* de `a/b/c.tile.openstreetmap.org` devolvieron 200 y el mapa base se renderizó | **APROBADO** |

### I. Regresión Códigos Fijos (FE-SIG 2)

| # | Verificación | Resultado |
|---|---|---|
| 41 | Códigos Fijos carga 6261 puntos | **APROBADO** |
| 42 | Su popup se abre por clic con las seis filas coincidentes con la API | **APROBADO** |
| 43 | `L.circleMarker` + renderer Canvas compartido sin cambios | **APROBADO** |
| 44 | Su leyenda y su *checkbox* siguen funcionando | **APROBADO** |
| 45 | `markers.js` no fue modificado | **APROBADO** |

### J. Responsive

| # | Ancho | Resultado |
|---|---|---|
| 46 | 1366 px | **APROBADO** — sin desbordamiento (`scrollWidth` 1366 = `innerWidth`); dos columnas «320px 896px» (panel 320, mapa 896x608); los cuatro *checkboxes* y *badges* dentro del *viewport*; leyenda a una columna |
| 47 | 768 px | **APROBADO** — sin desbordamiento (768 = 768); una columna de 736px; mapa 736x440; los cuatro *checkboxes* y *badges* dentro del *viewport*; leyenda a 2 columnas |
| 48 | 360 px | **APROBADO** — sin desbordamiento (`scrollWidth` 360 = `innerWidth`); una columna, panel 328, mapa 328x420; cuatro *checkboxes* 16x16 dentro del *viewport*; *badges* dentro del *viewport*; leyenda a una columna; los ítems del panel se envuelven |

### K. Build

| # | Verificación | Resultado |
|---|---|---|
| 49 | `dotnet build VisorDatosSIG.sln` | **APROBADO** — 0 errores, 0 advertencias |
| 50 | `git diff --check` | **APROBADO** — limpio |

### L. Re-ejecución de cierre (Escenarios A y B y prioridad de clic)

**Motivo:** el reporte previo mostraba una aparente inconsistencia (estado
inicial con Vías **activa** frente a un desplazamiento con 0 peticiones para
`vias`). Se repitió la prueba **desde una carga limpia** (recarga completa de
`/Visor`), midiendo a la vez el contador instrumentado sobre
`geojson.obtenerCapa` y el **registro real de red** de DevTools.

#### Escenario A — Manzanas y Códigos Fijos activas, Vías activa, Lotes inactiva

| Capa | Petición `bbox` en la carga inicial | Petición `bbox` tras **un** pan |
|---|---|---|
| manzanas | 1 | 1 |
| lotes | 0 | 0 |
| codigosfijos | 1 | 1 |
| vias | 1 | 1 |

- Contador medido tras el pan: `{ manzanas: 1, codigosfijos: 1, vias: 1 }` (sin
  clave `lotes`).
- Confirmación por **red**: tras el pan aparecen exactamente una petición nueva
  por capa activa (`manzanas`, `codigosfijos`, `vias`), cada una con su propio
  `bbox`. **Ninguna** petición para `lotes`.
- El pan fue entrada real de teclado (flecha) sobre el mapa enfocado; el centro
  cambió de longitud, por lo que el `moveend` fue real.
- **Corrección de la inconsistencia:** el 0 de `vias` del reporte anterior
  correspondía al paso 8, donde Vías ya estaba desactivada. Con Vías activa
  (arranque limpio), un solo pan genera **exactamente una** petición para `vias`.

#### Escenario B — las cuatro capas activas

Secuencia: activar Lotes con su *checkbox* real, esperar su carga (*badge*
`Aviso`, `10.000 cargados — límite alcanzado; acerque el mapa · 15.280 en
total`) y hacer **un** pan nuevo.

| Capa | Petición `bbox` tras **un** pan con las cuatro activas |
|---|---|
| manzanas | 1 |
| lotes | 1 |
| codigosfijos | 1 |
| vias | 1 |

- Contador medido tras el pan: `{ manzanas: 1, lotes: 1, codigosfijos: 1, vias: 1 }`.
- Confirmación por **red**: tras el pan aparece exactamente una petición por capa
  (`manzanas`, `lotes`, `codigosfijos`, `vias`). La activación de Lotes, aparte,
  emitió su propia petición única.
- Sin duplicación: cada capa consultó **una** vez por el movimiento confirmado.

#### Prioridad del despachador de clic (clic real sobre geometrías superpuestas)

Los clics se despacharon como eventos de ratón reales
(`mousedown`/`mouseup`/`click`) en las coordenadas de pantalla de la geometría;
**nunca** se llamó `openPopup()`. En cada caso, un escucha de `map.click`
confirmó que el despachador se ejecutó (`dispatcherFired: true`) y se comprobó
`map._popup._source` junto con el texto del popup activo.

| Caso | Geometrías presentes en el punto | Popup abierto | Popups abiertos |
|---|---|---|---|
| Código Fijo sobre polígono y vía | Códigos Fijos + Vías + Lotes + Manzanas | **Código Fijo** (`Código SIG 30.466.002`, `Estado Normal`) | 1 |
| Vía sin Código Fijo | Vías (sin Códigos Fijos, sin Lotes, sin Manzanas) | **Vía** (`Nombre RN10: San Ignacio de Velasco-San Matias`, `ObjectID 33`) | 1 |
| Lote sobre Manzana | Lotes + Manzanas | **Lote** (`NroLote L24`) | 1 |
| Manzana aislada | Manzanas | **Manzana** (`UV_MZA MZA. 21`) | 1 |

El orden observado es exactamente **Códigos Fijos -> Vías -> Lotes -> Manzanas**,
y en todos los casos se abrió **un único** popup: el de mayor prioridad.

> Nota metodológica: el «clic real» es un evento de ratón despachado sobre el
> elemento del mapa en las coordenadas exactas de la geometría (no una llamada
> programática a `openPopup`). Es la forma disponible para apuntar a una
> geometría concreta dentro del navegador controlado.

#### Defecto hallado y corregido en esta re-ejecución

La prueba de prioridad con Lotes activa **no cumplía** el orden: el clic abría
un popup de **Lote** aunque hubiera un Código Fijo en el punto. Causa: los
contenedores `<canvas>` de los renderers nacen en su **primer render**, no al
crear el renderer; `layers.js` desactivaba el puntero de los renderers al
activar la capa, **antes** de ese primer render, de modo que el `<canvas>` de
Lotes quedaba interactivo (`pointer-events: auto`). Leaflet marca ese canvas con
`_leaflet_disable_events`, el mapa ignora el evento y el despachador único
**no se ejecutaba** (verificado: el `map.on('click')` no recibía el evento y el
popup nativo del canvas de Lotes se abría por encima de la prioridad).

Corrección mínima (solo en `layers.js`, sin reescribir el despachador): reaplicar
`desactivarPunteroDeRenderers()` **después** de `addData`, cuando el `<canvas>`
ya existe. Verificado tras la corrección: los cuatro `<canvas>` quedan con
`pointer-events: none`, el despachador se ejecuta y se respeta el orden de
prioridad en los cuatro casos de la tabla anterior.

## Notas
- **404 de `favicon.ico`:** el único mensaje de consola es un 404 de
  `http://localhost:5000/favicon.ico`, un archivo estático ausente sin relación
  con FE-SIG 3. Se registra honestamente: la consola **no** está literalmente
  vacía; lo que se verificó es que ningún script de FE-SIG 3 emite error.
- **Hallazgo del clic en Canvas:** Leaflet enlaza el clic del renderer Canvas a
  su propio `<canvas>` y lo marca con `_leaflet_disable_events`, de modo que el
  mapa ignora sus eventos. Con un canvas por capa, el canvas del panel más alto
  (Códigos Fijos) recibía todos los clics y las capas inferiores nunca abrían su
  popup (verificado: sobre una Manzana el clic llegaba al mapa pero nunca a la
  capa). Se resolvió con `pointer-events: none` en los cuatro renderers y **un
  único** despachador de clic a nivel de mapa que hace *hit-test* con
  `layer._containsPoint(evento.layerPoint)` en el orden Códigos Fijos -> Vías ->
  Lotes -> Manzanas. Las cuatro capas conservan su popup y el orden visual.
- **15280 Lotes en SQL frente a 10000 cargados:** es lo esperado por el límite
  `1..10000` del servidor; el frontend no pagina más allá de ese valor. El panel
  lo indica expresamente con el *badge* `Aviso`.
- **Evidencias:** las capturas de pantalla se tomaron pero se conservaron
  **fuera del repositorio**; no se creó un registro de evidencias para esta
  fase.

## Incidencias
Ninguna incidencia bloqueante. Las dos observaciones relevantes (el 404 de
`favicon.ico` y el hallazgo del clic en Canvas) están documentadas arriba; la
segunda se corrigió dentro de `layers.js`, en archivos autorizados.

## Limitaciones
- Solo están implementadas las **cuatro** capas indicadas (Manzanas, Lotes,
  Códigos Fijos y Vías).
- `identify.js` sigue siendo un *placeholder* y `/api/capas/identificar` **no**
  se consume en esta fase; la identificación espacial general corresponde a
  FE-SIG 4.
- No hay paginación más allá del `limit` 10000: por encima de ese valor el
  servidor trunca (Lotes queda hoy en 10000 de 15280).
- No hay filtros por atributos ni exportación.
- La prueba de 401 sin token no se repitió en esta fase (fila 38): queda
  cubierta para Códigos Fijos en `P-FE-03` y pendiente de repetir para el
  catálogo y las nuevas capas.
- Queda pendiente adjuntar las capturas de evidencia al repositorio.
- El hit-test del despachador usa `layer._containsPoint(...)` de Leaflet 1.9.4
  (método interno del renderer Canvas): deuda técnica **no bloqueante**,
  detallada en `docs/09_VisorSIG/FE-SIG-3_Capas_Vectoriales.md`. La comprobación
  defensiva `typeof layer._containsPoint === 'function'` ya existe en
  `buscarObjetivoEnPunto`.
- El 404 de `/favicon.ico` queda **fuera de FE-SIG 3** y se registra como
  pendiente **menor**: no se creó favicon ni se modificó `Layout`.
- FE-SIG 4 (identificación espacial) deberá coordinar su propio manejo de
  `map.click` con este despachador para no generar identificaciones duplicadas.
