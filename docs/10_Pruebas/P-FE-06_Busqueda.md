# P-FE-06 — Búsqueda catastral real (FE-SIG 5)

- **Fase:** FE-SIG 5
- **Módulo:** `src/VisorDatosSIG.Web`
- **Tipo:** prueba manual end-to-end (API real + UI real)
- **Estado:** **APROBADO** en los escenarios ejecutados.

> Solo se declara **APROBADO** lo respaldado por observaciones reales. Lo no
> ejecutado se etiqueta **PENDIENTE DE EJECUCIÓN**. No se inventan resultados.

## Precondiciones

- Rama `dev-josias`.
- **API:** `http://localhost:5080` (backend real; **no** se modificó).
- **Web:** `http://localhost:5000`. Se **detuvo solo** `VisorDatosSIG.Web` para
  compilar (bloqueaba el *apphost*), se ejecutó `dotnet build VisorDatosSIG.sln` y
  se relevantó con el mismo comando:
  `dotnet run --project src\VisorDatosSIG.Web\VisorDatosSIG.Web.csproj --no-build --urls http://localhost:5000`.
- SQL Server con las cuatro capas.
- Sesión `admin` validada: `/Visor` (protegido) cargó y el *guard* validó contra
  `/me` (el navegador ya tenía una sesión vigente; no se reescribió el login).
- Conexión a Internet (mapa base OpenStreetMap).

## 1. Contrato real del endpoint (API)

Consultado autenticado antes de codificar.

| # | Verificación | Resultado real |
|---|---|---|
| 1 | `GET /api/catastro/buscar?q=30.445&pagina=1&limite=20` con JWT | **APROBADO** — `200`; `totalRegistros:1`, `totalPaginas:1`, `datos:[{Capa:"CodigosFijos", Clave:"1", Titulo:"30.445.002", Subtitulo:"AGUILAR SOCORE AGUSTIN EUGENIO"}]` |
| 2 | Casing del nivel superior | **APROBADO** — camelCase: `pagina`, `limite`, `totalRegistros`, `totalPaginas`, `datos` |
| 3 | Casing de cada item | **APROBADO** — PascalCase: `Capa`, `Clave`, `Titulo`, `Subtitulo` |
| 4 | `GET ...?q=30` **sin** JWT | **APROBADO** — `401 Unauthorized` |
| 5 | `GET ...?q=` (vacío) directo a la API | **APROBADO** — `400` |
| 6 | Término sin coincidencias (`q=ZZZZNOEXISTE99`) | **APROBADO** — `200`; `totalRegistros:0`, `datos:[]` |
| 7 | `limite=101` (fuera de 1..100) | **APROBADO** — `400` |
| 8 | `limite=0` | **APROBADO** — `400` |
| 9 | `pagina=0` | **APROBADO** — `400` |
| 10 | `capa=vias` (minúscula) | **APROBADO** — `200`; la colación de SQL Server es insensible a mayúsculas |

## 2. Módulo y UI (navegador)

| # | Verificación | Resultado real |
|---|---|---|
| 11 | `window.VisorSIG.search` expone `inicializar`/`buscar`/`limpiar` | **APROBADO** |
| 12 | `window.VisorSIG.capas.buscarFeatureCargada` es función | **APROBADO** |
| 13 | Existen `#panel-busqueda`, `#form-busqueda`, `#texto-busqueda`, `#filtro-capa`, `#btn-buscar`, `#btn-limpiar-busqueda`, `#estado-busqueda`, `#resultados-busqueda`, `#paginacion-busqueda` | **APROBADO** — los nueve presentes |
| 14 | Búsqueda vacía: no se emite petición y se muestra el aviso | **APROBADO** — estado `Escriba un término de búsqueda.`, `0` peticiones |

## 3. Búsquedas reales por tipo (UI)

Todas se ejecutaron enviando el formulario real.

| # | Término / filtro | Resultado real |
|---|---|---|
| 15 | `30.445` (Todas) | **APROBADO** — `1 resultado encontrado`; tarjeta `Códigos Fijos · 30.445.002 · AGUILAR SOCORE AGUSTIN EUGENIO · Clave 1`. Petición exacta: `/buscar?q=30.445&pagina=1&limite=20` |
| 16 | `M-1` (Manzanas) | **APROBADO** — primera tarjeta `M-1` (Clave 768) |
| 17 | `L07` (Lotes) | **APROBADO** — `471 resultados encontrados`; primera tarjeta `Lote: L07` |
| 18 | `1` (Códigos Fijos) | **APROBADO** — `4963 resultados encontrados`; primera tarjeta `1.000.002` |
| 19 | `secondary` (Vías) | **APROBADO** — primera tarjeta `-` (Nombre nulo), `Subtitulo: Tipo: secondary, OSM: ...` |
| 20 | `ZZZZNOEXISTE99` | **APROBADO** — `No se encontraron resultados.`, `0` tarjetas |

## 4. Filtros de capa (UI)

| # | Filtro | Petición real | Resultado |
|---|---|---|---|
| 21 | Todas las capas | *sin `capa`* | **APROBADO** — `9965 resultados encontrados` (q=`1`) |
| 22 | Manzanas | `...&capa=Manzanas` | **APROBADO** — `260 resultados encontrados` |
| 23 | Lotes | `...&capa=Lotes` | **APROBADO** — `4180 resultados encontrados` |
| 24 | Códigos Fijos | `...&capa=CodigosFijos` | **APROBADO** — `4963 resultados encontrados` |
| 25 | Vías | `...&capa=Vias` | **APROBADO** — `541 resultados encontrados` |
| 26 | Cambiar filtro con el campo **vacío** | — | **APROBADO** — `0` peticiones; el estado no cambia |

## 5. Paginación

| # | Verificación | Resultado real |
|---|---|---|
| 27 | `q=1&capa=Lotes` → paginación visible | **APROBADO** — `Anterior · Página 1 de 209 · Siguiente`; `Anterior` deshabilitado, `Siguiente` habilitado |
| 28 | Clic en **Siguiente** | **APROBADO** — petición `...&pagina=2...`; `Página 2 de 209`; ambos habilitados |
| 29 | Clic en **Anterior** | **APROBADO** — vuelve a `Página 1 de 209` |
| 30 | Se conservan término y filtro al paginar | **APROBADO** — `q=1&capa=Lotes` en ambas páginas |
| 31 | No se generan cientos de botones | **APROBADO** — solo `Anterior` y `Siguiente` |

## 6. Carrera de búsquedas

Se retrasó artificialmente la respuesta de `q=30` (1500 ms) y se lanzó después
`q=30.445`:

| # | Verificación | Resultado real |
|---|---|---|
| 32 | Solo la última respuesta se pinta | **APROBADO** — estado final `1 resultado encontrado` con título `30.445.002` (la respuesta tardía de `30` no reemplazó a la segunda) |

## 7. Localización en el mapa («Ver en mapa»)

| # | Escenario | Resultado real |
|---|---|---|
| 33 | Punto cargado (Código Fijo `30.445.002`, `id=1`) | **APROBADO** — botón «Ver en mapa»; `panTo` (centro cambió); popup local persistente: `Código SIG 30.445.002 · Código fijo 5788 · Nombre AGUILAR SOCORE AGUSTIN EUGENIO · Estado Normal · Lote — · ID interno 1` |
| 34 | Polígono cargado (Manzana `M-1`, `id=768`) | **APROBADO** — `fitBounds` (zoom `13 → 18`); popup `UV_MZA M-1 · UV — · MZA M-1 · ID interno 768` |
| 35 | Línea cargada (Vía `secondary`, `id=165`) | **APROBADO** — `fitBounds`; popup `Nombre — · TipoVia secondary · OSMID 160460027 · OBJECTID 165 · ID interno 165` |
| 36 | Popup tras localizar | **APROBADO** — permanece abierto más de 2,9 s (se reabre en la nueva instancia tras el `moveend`/recarga de FE-SIG 3) |
| 37 | Resultado NO cargado (Lotes **desactivado**, `q=L07`) | **APROBADO** — `471 resultados`; `0` botones «Ver en mapa`, `20/20` tarjetas muestran «Fuera de la vista actual». La búsqueda funciona aunque Lotes esté desactivado |
| 38 | `buscarFeatureCargada` en capas activas | **APROBADO** — Manzanas `id=1` → encontrada; Códigos Fijos `id=1` → encontrada; Vías `id=1` → encontrada |
| 39 | `buscarFeatureCargada('lotes', 1)` con Lotes inactivo | **APROBADO** — `null` (capa no activa) |

## 8. Regresión FE-SIG 3 / FE-SIG 4

| # | Verificación | Resultado real |
|---|---|---|
| 40 | Único *listener* de clic del mapa | **APROBADO** — `map._events.click.length === 1` |
| 41 | Identificar sigue funcionando con clic real | **APROBADO** — activado; clic real → `1` petición `GET /api/capas/identificar`; panel visible; `No se encontraron elementos en este punto.` (punto central sin entidades) |
| 42 | En modo Identificar no se abre popup local simultáneo | **APROBADO** — `0` `.leaflet-popup` tras el clic |
| 43 | «Ver en mapa» con Identificar activo | **APROBADO** — Identificar se desactiva automáticamente (`estaActivo()` → `false`) antes de abrir el popup local |
| 44 | Pan/zoom y capas siguen operativos | **APROBADO** — `panTo`/`fitBounds` reales movieron la vista y recargaron las capas activas |

## 9. Seguridad y red

| # | Verificación | Resultado real |
|---|---|---|
| 45 | La petición de búsqueda lleva `Authorization: Bearer` | **APROBADO** — usada vía `api.peticion(..., protegida:true)`; sin token la API respondió `401` (ver #4) |
| 46 | `401` conserva el flujo de sesión de FE-AUTH | **APROBADO** — `search.js` retorna sin mensaje propio en `status === 401`; el flujo lo resuelven `api.js`/`guard.js` |
| 47 | Sin SQL ni datos JSON locales en el frontend | **APROBADO** — toda la búsqueda se resuelve con la llamada al endpoint real |

## 10. Consola

| # | Verificación | Resultado real |
|---|---|---|
| 48 | Sin errores JavaScript propios de FE-SIG 5 | **APROBADO** — el único error de consola es el `404` de `favicon.ico` (conocido y ajeno a FE-SIG 5) |

## 11. Responsive

Medido con emulación de viewport CSS real:

| # | Ancho | `innerWidth` | `scrollWidth` | Overflow | Layout / medidas reales | Resultado |
|---|---|---|---|---|---|---|
| 49 | 1366 | 1366 | 1366 | No | `.visor-shell` 2 columnas (`320px 896px`); panel 286; mapa 896×608 | **APROBADO** |
| 50 | 768 | 768 | 768 | No | 1 columna (`736px`); panel 702; input/select dentro; mapa 736×440 | **APROBADO** |
| 51 | 360 | 360 | 360 | No | 1 columna (`328px`); panel 294; botones apilados (`flex-direction: column`); mapa 328×420 | **APROBADO** |
| 52 | 360 — lista larga | — | — | — | Resultados desplazables (`clientHeight 300 < scrollHeight 3613`); paginación visible y compacta | **APROBADO** |

## 12. Build y git

| # | Verificación | Resultado real |
|---|---|---|
| 53 | `dotnet build VisorDatosSIG.sln` | **APROBADO** — `0` errores, `0` advertencias (tras detener solo `VisorDatosSIG.Web` y relevantarlo) |
| 54 | `git diff --check` | **APROBADO** — sin errores de espacios |

## Capturas

Las capturas se conservaron **fuera del repositorio**
(`%TEMP%\opencode\fe-sig5_visor_1366.webp` y `fe-sig5_visor_360.webp`).

## Limitaciones observadas

- La localización en el mapa solo está disponible si la entidad ya está cargada
  en la capa Leaflet actual; el endpoint no entrega geometría por resultado.
- No se activan capas automáticamente para localizar.
- El popup local se reabre tras la recarga por `moveend` de FE-SIG 3 (manejador
  `layeradd` con limpieza a los 4 s); si en ese lapso no llega la recarga, el
  popup abierto por el clic permanece.

## Pendiente de ejecución

- `401` en la **UI** de búsqueda mediante invalidación manual del JWT (el `401`
  se verificó a nivel de endpoint en #4; el comportamiento de `search.js` en esa
  rama se revisó por código).
- Regresión de **logout** por UI en esta fase (ya cubierta en fases previas).
