# FE-SIG 5 — Búsqueda catastral real

- **Fase:** FE-SIG 5 (CU08, CU09, CU10, CU11, CU13 — búsqueda alfanumérica)
- **Módulo:** `src/VisorDatosSIG.Web`
- **Rama:** `dev-josias`
- **Estado:** implementado y verificado end-to-end contra la API real.

## 1. Objetivo

Permitir la búsqueda alfanumérica **real** sobre las capas catastrales
(Manzanas, Lotes, Códigos Fijos y Vías) desde el visor Web, consumiendo el
endpoint protegido del backend que consulta **SQL Server**, sin mocks, sin
filtrado local que sustituya al backend, sin datos JSON locales y sin SQL desde
JavaScript.

Flujo: el usuario escribe un término → `GET api/catastro/buscar` → SQL Server →
resultados paginados → panel del visor.

## 2. Endpoint consumido

```
GET api/catastro/buscar?q=<término>&capa=<Capa>&pagina=<n>&limite=20
```

- Endpoint protegido con `[Authorize]`: **exige JWT**. Sin token responde `401`.
- `q` es **obligatorio**. `capa` es opcional (omitir = todas las capas).
- `pagina` ≥ 1 y `limite` entre 1 y 100 (validado por `[Range]`). El frontend usa
  inicialmente `pagina = 1` y `limite = 20`.
- Con `q` vacío o en blanco la API responde `400`.

## 3. Contrato JSON real observado

Verificado contra la API real en `http://localhost:5080` antes de codificar.
Nivel superior en **camelCase**; cada item conserva **PascalCase** (es un
`Dictionary`):

```json
{
  "pagina": 1,
  "limite": 20,
  "totalRegistros": 1,
  "totalPaginas": 1,
  "datos": [
    {
      "Capa": "CodigosFijos",
      "Clave": "1",
      "Titulo": "30.445.002",
      "Subtitulo": "AGUILAR SOCORE AGUSTIN EUGENIO"
    }
  ]
}
```

`TotalPaginas` se calcula en el backend como
`ceil(totalRegistros / limite)`. Cuando no hay coincidencias, `totalRegistros` es
`0` y `datos` es `[]`.

La lectura de campos en el frontend es **tolerante al casing**
(`valorCampo` compara sin distinguir mayúsculas/minúsculas), de modo que un
cambio de serialización no rompe la vista.

## 4. Campos realmente buscables por capa

Coinciden con la consulta SQL real del backend. **No** se presentan campos como
buscables si el backend no los busca.

| Capa (`capa`) | Campos buscados (LIKE) | `Titulo` | `Subtitulo` |
|---|---|---|---|
| `Manzanas` | `UV_MZA`, `UV`, `MZA` | `UV_MZA` | `UV: <UV>, MZA: <MZA>` |
| `Lotes` | `NroLote` | `Lote: <NroLote>` | `Origen: <IdOrigen>` |
| `CodigosFijos` | `CodF_SIG`, `CodFijo`, `Nombre` | `CodF_SIG` | `Nombre` |
| `Vias` | `Nombre`, `TipoVia`, `OSMID` | `Nombre` (o `-`) | `Tipo: <TipoVia>, OSM: <OSMID>` |

`Clave` es el identificador interno serializado como texto (`IdManzana`,
`IdLote`, `IdCodigo`, `IdVia`), el mismo `id` que usa el GeoJSON de capas.

## 5. Autenticación

La petición se hace **exclusivamente** con `window.VisorSIG.api.peticion(...)`
(`js/visor/search.js`), con:

- `metodo: 'GET'`
- `token: window.VisorSIG.sesion.obtenerToken()`
- `protegida: true`

No se usa `fetch` directamente. Un `401` deja que FE-AUTH resuelva la sesión:
`api.js` limpia la sesión y emite el evento; `guard.js` redirige al Login.
`search.js` **no** crea redirecciones propias ni pisa ese flujo.

## 6. Filtro por capa

`#filtro-capa` ofrece:

| Texto visible | `value` (query real) |
|---|---|
| Todas las capas | *(vacío — se omite `capa`)* |
| Manzanas | `Manzanas` |
| Lotes | `Lotes` |
| Códigos Fijos | `CodigosFijos` |
| Vías | `Vias` |

El texto puede llevar acentos; la query usa los nombres reales del backend. Al
cambiar el filtro **no** se busca automáticamente si el campo está vacío. Si hay
término, se reinicia a `pagina = 1` y se vuelve a buscar.

## 7. Paginación

- Se usan `pagina` y `totalPaginas` de la respuesta.
- Controles: **Anterior** · `Página X de Y` · **Siguiente**.
- `Anterior` deshabilitado en la primera página; `Siguiente` deshabilitado en la
  última. No se generan cientos de botones.
- Al cambiar de página se conservan término y filtro.
- El panel de resultados vuelve al inicio (`scrollTop = 0`); no se fuerza scroll
  del mapa.

## 8. Protección contra respuestas obsoletas (carrera)

Contador `secuencia`:

```js
secuencia += 1;
var actual = secuencia;
// ...
consultar(...).then(function (resultado) {
    if (actual !== secuencia) { return; } // respuesta antigua
    aplicarRespuesta(resultado);
});
```

Si el usuario busca "30" y rápidamente "30.445", la respuesta de "30" **nunca**
reemplaza a la de "30.445". Al limpiar también se incrementa `secuencia`.

## 9. Validación y envío

- **No** se envían búsquedas vacías: `trim()`; si queda vacío se muestra
  «Escriba un término de búsqueda.» y **no** se llama a la API.
- No se impone un mínimo de caracteres que el backend no exija.
- Se busca solo en: `submit`, cambio de filtro (con término) y paginación.
  **No** se busca en cada tecla.

## 10. Estados del panel

| Situación | Mensaje (`#estado-busqueda`) |
|---|---|
| Antes de buscar | *(vacío)* |
| Cargando | «Buscando...» |
| Sin coincidencias | «No se encontraron resultados.» |
| Error | «No fue posible realizar la búsqueda.» |
| Éxito | «1 resultado encontrado» / «N resultados encontrados» |

El número del éxito viene de `totalRegistros`, **nunca** de `datos.length`.

## 11. Resultados

Cada tarjeta muestra **Capa**, **Titulo**, **Subtitulo** y la **Clave**.
Seguridad del DOM: todo se construye con `document.createElement` y
`textContent`; **nunca** se usa `innerHTML` con datos del servidor. Los valores
nulos/vacíos se muestran como `—`; el guion `-` que ya envía el backend se
conserva.

## 12. Integración con capas (localización solo si ya está cargada)

El endpoint de búsqueda **no devuelve geometría** (ni lat/lng ni bbox) por
resultado. Por eso **no** se inventan coordenadas: la única mejora en el mapa es
la acción «Ver en mapa», disponible **solo si la entidad ya está cargada en la
capa Leaflet actual**.

Para ello `layers.js` expone un método de solo lectura:

```js
window.VisorSIG.capas.buscarFeatureCargada(claveCapa, id)
```

- Mapea la capa (`Manzanas → manzanas`, `Lotes → lotes`,
  `CodigosFijos → codigosfijos`, `Vias → vias`).
- Recorre **solo** los layers ya cargados del grupo y compara `feature.id` con
  `id` (la `Clave` de búsqueda es el mismo identificador como texto).
- Devuelve el layer Leaflet o `null`. **No** hace peticiones, **no** expone
  `ESTADO` mutable y **no** carga capas.
- Requiere que la capa esté **activa**: una capa desactivada no está en el mapa y
  su popup local no podría mostrarse.

La búsqueda **no** depende de que una capa esté activa: los resultados llegan del
backend igualmente. Solo la localización depende de la carga actual.

## 13. «Ver en mapa» y «Fuera de la vista actual»

Si el resultado está cargado:

- Punto: `map.panTo(layer.getLatLng())`.
- Polígono/línea: `map.fitBounds(layer.getBounds(), { padding: [30, 30], maxZoom: 18 })`.
- Después se abre el popup local (FE-SIG 2/3) con `layer.openPopup()`.
- Si el modo **Identificar** está activo, se **desactiva** antes de abrir el
  popup local, para no ofrecer una UX contradictoria. No se crea resaltado
  permanente.

Detalle relevante observado: el `panTo`/`fitBounds` dispara el `moveend` de
FE-SIG 3, que recarga las capas activas y **recrea** sus geometrías; la instancia
anterior se destruye y con ella su popup. Para que el popup permanezca,
`search.js` vuelve a abrirlo en la **nueva** instancia de la misma entidad a
través del evento `layeradd` del grupo de la capa, con un manejador que se retira
al primer acierto y, como máximo, a los 4 s.

Si el resultado **no** está cargado, no se intenta resolverlo artificialmente: se
muestra la etiqueta «Fuera de la vista actual».

## 14. Limitación real del backend

El endpoint de búsqueda actual **no entrega geometría ni extensión por
resultado**. Para una localización **garantizada** se requerirá posteriormente:

- un endpoint por **ID**, o
- un endpoint de **extensión/geometry** de la entidad.

En FE-SIG 5 **no** se modificó el backend.

## 15. Responsive

Se reutilizan los tokens de FE-UI 1; solo se añaden estilos de formulario, input,
select, botones, estado, resultados, tarjeta, metadatos, paginación, «Ver en
mapa» y «Fuera de la vista actual». Verificado sin desbordamiento horizontal a
1366 / 768 / 360 px (ver `P-FE-06`). A 360 los botones se apilan, la lista de
resultados es desplazable (`max-height`) y la paginación se mantiene compacta.

## 16. API pública

- `window.VisorSIG.search`: `inicializar()`, `buscar(pagina)`, `limpiar()`.
- `window.VisorSIG.capas.buscarFeatureCargada(claveCapa, id)`: nueva, de solo
  lectura.

`search.js` **no** registra `map.on('click')` ni eventos globales innecesarios;
no modifica la lógica del despachador de FE-SIG 3/4.

## 17. Pruebas

Ver `docs/10_Pruebas/P-FE-06_Busqueda.md` (solo resultados ejecutados realmente).

## 18. Limitaciones

- La localización en el mapa solo funciona si la entidad ya está cargada en la
  capa Leaflet actual (el endpoint no entrega geometría por resultado).
- No se activan capas automáticamente para localizar un resultado.
- No se resaltan de forma permanente los resultados.
- Depende de la disponibilidad de la API y de SQL Server.
- Solo aplica a las cuatro capas implementadas (Manzanas, Lotes, Códigos Fijos,
  Vías).
