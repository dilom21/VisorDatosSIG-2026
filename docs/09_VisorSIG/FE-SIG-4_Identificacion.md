# FE-SIG 4 — Identificación de elementos geográficos

- **Fase:** FE-SIG 4 (CU17 — Identificar Elemento)
- **Módulo:** `src/VisorDatosSIG.Web`
- **Rama:** `dev-josias`
- **Estado:** implementado y verificado end-to-end contra la API real.

## 1. Objetivo

Permitir que el usuario identifique las entidades geográficas que caen bajo un
clic en el mapa, usando el **endpoint espacial real** del backend
(`STIntersects` de SQL Server), sin mocks, sin reconstruir geometrías desde
JavaScript y sin acceso directo a SQL.

## 2. Endpoint consumido

```
GET api/capas/identificar?lng=<longitud>&lat=<latitud>&tolerancia=10
```

- Endpoint protegido con `[Authorize]`: **exige JWT**. Sin token responde `401`.
- El backend consulta las **cuatro** tablas espaciales (`dbo.CodigosFijos`,
  `dbo.Lotes`, `dbo.Manzanas`, `dbo.Vias`) y devuelve hasta **5** entidades por
  capa.
- Cada resultado es un `Feature` GeoJSON con `id`, `geometry` y `properties`.
  `properties._Capa` indica la capa de origen (`CodigosFijos`, `Lotes`,
  `Manzanas`, `Vias`).

## 3. Autenticación

La petición se hace **exclusivamente** con `window.VisorSIG.api.peticion(...)`
(`js/visor/identify.js`), con:

- `metodo: 'GET'`
- `token: window.VisorSIG.sesion.obtenerToken()`
- `protegida: true`

No se usa `fetch` directamente. Un `401` dispara el flujo de sesión existente de
FE-AUTH (`api.js` limpia la sesión y emite el evento; `guard.js` redirige al
Login). `identify.js` no pisa ese flujo con un mensaje propio.

## 4. Tolerancia

La tolerancia está **fija en 10 metros** (`TOLERANCIA_METROS = 10`). El usuario
no puede escribir una tolerancia arbitraria en esta fase.

## 5. Integración con el despachador único de clic

`layers.js` conserva el **único** `map.on('click', despacharClic)` del visor.
`identify.js` **no registra ningún `map.on('click')`**: no hay dos sistemas
compitiendo.

El despachador integra FE-SIG 4 con la mínima modificación posible:

```js
function despacharClic(evento) {
    if (!evento || !evento.layerPoint) {
        return;
    }
    if (ns.identify && typeof ns.identify.estaActivo === 'function'
        && ns.identify.estaActivo()
        && typeof ns.identify.manejarClic === 'function') {
        ns.identify.manejarClic(evento);
        return;
    }
    // ... hit-test local FE-SIG 3 sin cambios
}
```

- **Modo normal:** se ejecuta el hit-test local de FE-SIG 3
  (`_containsPoint`, orden Códigos Fijos → Vías → Lotes → Manzanas) y se abre el
  popup local.
- **Modo Identificar:** el clic se delega a `identify.manejarClic` y **no** se
  abre ningún popup local.

`_containsPoint` y todo el despachador de FE-SIG 3 permanecen encapsulados en
`layers.js`; la deuda técnica de Leaflet 1.9.4 se conserva documentada allí.

## 6. Modo normal vs. modo Identificar

El modo Identificar **no está siempre activo**. Se controla con el botón
`#btn-identificar`:

| Estado | `aria-pressed` | Cursor | Panel | Clic en el mapa |
|---|---|---|---|---|
| Inactivo | `false` | normal | oculto | popup local FE-SIG 3 |
| Activo | `true` | `crosshair` | visible | endpoint `/identificar` |

- Al activar: clase `visor-map--identificando` en el contenedor del mapa
  (cursor `crosshair`), `aria-pressed="true"`, panel visible con la ayuda
  «Haga clic sobre el mapa para identificar elementos.».
- Al desactivar: `aria-pressed="false"`, cursor normal, panel oculto y **ninguna**
  petición `/identificar`.

## 7. Filtro por capas activas

El backend identifica sobre las **cuatro** tablas, pero el usuario no debe recibir
resultados de capas que tenga apagadas. Tras recibir la respuesta, el frontend
**filtra** según `window.VisorSIG.capas.estaActiva(clave)` — API pública mínima de
`layers.js` que no expone el estado interno:

| `properties._Capa` | `clave` |
|---|---|
| `Manzanas` | `manzanas` |
| `Lotes` | `lotes` |
| `CodigosFijos` | `codigosfijos` |
| `Vias` | `vias` |

La comparación es tolerante a mayúsculas/minúsculas y acentos. Si una capa está
desactivada, sus resultados **no** se muestran. No se vuelve a consultar ningún
dato: solo se filtra la respuesta ya recibida del endpoint.

## 8. Orden de resultados

Agrupados por capa en el orden visual de FE-SIG 3:

1. Códigos Fijos — Código SIG, Código fijo, Nombre, Estado, Lote, ID
2. Vías — Nombre, Tipo de vía, OSMID, OBJECTID, ID
3. Lotes — NroLote, IdManzana, IdOrigen, ID
4. Manzanas — UV_MZA, UV, MZA, IdOrigen, ID

Los valores nulos/vacíos se muestran como `—`. El `Estado` se traduce a su nombre
(Normal, Para Corte, …) mediante `markers.obtenerNombreEstado`.

## 9. Seguridad del DOM

Todo el panel se construye con `document.createElement` y `textContent`. **Nunca**
se usa `innerHTML` con datos del servidor.

## 10. Carrera de peticiones (clics rápidos)

Se usa un contador `secuencia`:

```js
secuencia += 1;
var actual = secuencia;
// ...
consultar(lng, lat).then(function (resultado) {
    if (actual !== secuencia) { return; } // respuesta antigua
    ...
});
```

Al activar y al desactivar también se incrementa `secuencia`, de modo que una
respuesta en vuelo no pinta nada tras desactivar el modo. No se usa
`AbortController` adicional.

## 11. Coordenadas

Se muestran las coordenadas del **clic** (`latlng`), no las de `properties`:

- `evento.latlng.lng` → `lng`
- `evento.latlng.lat` → `lat`

Presentación con máximo 6 decimales (`toFixed(6)`). La **petición** usa la
precisión numérica real del evento (no se redondea la consulta).

## 12. Responsive

El botón y el panel reutilizan los tokens CSS de FE-UI 1. Verificado sin
desbordamiento horizontal a 1366 / 768 / 360 px (ver `P-FE-05`). Los resultados se
limitan con `max-height: 340px; overflow-y: auto` para no comprometer el mapa.

## 13. Cambios de capa tras identificar

El panel conserva los resultados de la última identificación e incluye una nota
que aclara que corresponden al clic con el estado de capas de ese momento. La
**siguiente** identificación aplica el estado de capas vigente. (Opción elegida
por ser la más simple y segura: no acopla `layers.js` a `identify.js`.)

## 14. API pública

- `window.VisorSIG.identify`: `inicializar()`, `activar()`, `desactivar()`,
  `estaActivo()`, `manejarClic(evento)`.
- `window.VisorSIG.capas.estaActiva(clave)`: ya existía; se usa para el filtro.

## 15. Pruebas

Ver `docs/10_Pruebas/P-FE-05_Identificacion.md` (solo resultados ejecutados
realmente).

## 16. Limitaciones

- Tolerancia fija en 10 m (no editable por el usuario en esta fase).
- El panel no re-identifica automáticamente al cambiar capas; se documenta en la
  nota del panel.
- No se dibujan geometrías ni se resaltan los elementos identificados.
- La identificación depende de la disponibilidad de la API y de SQL Server.
- Solo aplica a las cuatro capas implementadas (Manzanas, Lotes, Códigos Fijos,
  Vías).
