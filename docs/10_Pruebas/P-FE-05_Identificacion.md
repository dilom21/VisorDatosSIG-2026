# P-FE-05 — Identificación de elementos geográficos (FE-SIG 4)

- **Fase:** FE-SIG 4 (CU17)
- **Módulo:** `src/VisorDatosSIG.Web`
- **Tipo:** prueba manual de funcionamiento end-to-end
- **Estado:** **APROBADO** en los escenarios ejecutados.

> Solo se declara **APROBADO** lo respaldado por observaciones reales. Lo que no
> se ejecutó se etiqueta **PENDIENTE DE EJECUCIÓN**. No se inventan resultados.

## Precondiciones

- Rama `dev-josias`.
- **API:** `http://localhost:5080` (backend real, ejecutado desde el worktree del
  compañero; no se modificó).
- **Web:** `http://localhost:5000` (relanzada desde `dev-josias` tras recompilar).
- SQL Server con las cuatro capas: Manzanas 863, Lotes 15280, Códigos Fijos 6261,
  Vías 578.
- Usuario de desarrollo `admin`.
- Conexión a Internet (mapa base OpenStreetMap).

## 1. Endpoint real (API)

| # | Verificación | Resultado |
|---|---|---|
| 1 | `GET /api/capas/identificar?lng=-60.96&lat=-16.38&tolerancia=10` **sin** JWT | **APROBADO** — `401 Unauthorized` |
| 2 | `POST /api/autenticacion/iniciar` con `admin` → 200 y `accessToken` | **APROBADO** |
| 3 | `GET /api/capas/identificar?...` con `Bearer` → 200 con arreglo de `Feature` | **APROBADO** |
| 4 | `properties._Capa` presente en cada *feature* con el nombre de la tabla | **APROBADO** |
| 5 | Punto `(-60.9570, -16.3750)`: 6 elementos → `Lotes=3 Manzanas=2 Vias=1` | **APROBADO** |
| 6 | Punto `(-60.9600, -16.3800)`: 2 elementos → `Lotes=1 Manzanas=1` | **APROBADO** |
| 7 | Punto de Código Fijo `(-60.9560697, -16.3769032)`: 5 elementos → `CodigosFijos=1 Lotes=2 Manzanas=1 Vias=1` | **APROBADO** |

### Estructura de capas consultadas por el backend

| # | Verificación | Resultado |
|---|---|---|
| 8 | El backend consulta las **cuatro** capas (`CodigosFijos`, `Lotes`, `Manzanas`, `Vias`) en una sola llamada | **APROBADO** — observado en la respuesta real (las cuatro `_Capa` aparecen en los puntos de prueba) |
| 9 | El frontend filtra por **visibilidad** de capas (no el backend) | **APROBADO** — ver sección 4 |

## 2. Integración con el despachador de clic

| # | Verificación | Resultado |
|---|---|---|
| 10 | `identify.js` **NO** crea otro `map.on('click')` | **APROBADO** — `map._events.click.length === 1` antes y después de cargar `identify.js` |
| 11 | `_containsPoint` continúa encapsulado en `layers.js` | **APROBADO** — el hit-test local no se movió; FE-SIG 4 solo delegó desde `despacharClic` |
| 12 | En modo normal (identify inactivo) el comportamiento es idéntico a FE-SIG 3 | **APROBADO** — el clic abre el popup local y no se emite ninguna petición `/identificar` |
| 13 | El despachador único sigue siendo el de `layers.js` | **APROBADO** — `despacharClic` delega y retorna cuando identify está activo |

## 3. Modo Identificar (UI)

| # | Verificación | Resultado |
|---|---|---|
| 14 | `#btn-identificar` existe; `aria-pressed="false"` inicial | **APROBADO** |
| 15 | Al activar: `aria-pressed="true"` | **APROBADO** |
| 16 | Al activar: clase `visor-map--identificando` presente (cursor `crosshair`) | **APROBADO** |
| 17 | Al activar: panel visible y ayuda «Haga clic sobre el mapa para identificar elementos.» | **APROBADO** |
| 18 | Estado intermedio «Identificando...» durante la petición | **APROBADO** |
| 19 | Al desactivar: `aria-pressed="false"`, cursor normal, panel oculto | **APROBADO** |
| 20 | Ninguna petición `/identificar` con el modo inactivo | **APROBADO** — 0 peticiones tras desactivar y hacer clic |

## 4. Filtro por capas activas (caso obligatorio)

Mismo punto `(-60.9600, -16.3800)` en ambos estados:

| # | Estado de Lotes | Resultado observado | Resultado |
|---|---|---|---|
| 21 | **Desactivada** | Panel: `2 elementos encontrados` → grupos `Vías (1)`, `Manzanas (1)`. El backend había devuelto también Lotes; el frontend lo **filtró** | **APROBADO** |
| 22 | **Activada** | Panel: `3 elementos encontrados` → grupos `Vías (1)`, `Lotes (1)`, `Manzanas (1)` | **APROBADO** |

No se volvió a consultar ningún dato de capa para lograrlo: solo se filtró la
respuesta del endpoint `identificar`.

## 5. Resultados (orden y campos)

Punto de Código Fijo con 5 elementos devueltos:

| # | Verificación | Resultado |
|---|---|---|
| 23 | Orden visual de grupos: **Códigos Fijos → Vías → Lotes → Manzanas** | **APROBADO** |
| 24 | Código Fijo real: Código SIG `70.250.001`, Código fijo `1943`, Nombre `DIOCESIS de SAN IGNACIO de VELASCO`, Estado `Normal`, Lote `—`, ID `1240` | **APROBADO** |
| 25 | Se mostraron `5 elementos encontrados` (coincide con la respuesta real) | **APROBADO** |
| 26 | Valores `null` mostrados como `—` | **APROBADO** — campo «Lote» (IdLote null en la respuesta) |
| 27 | Vía, Lote y Manzana reales con sus campos (Nombre/Tipo/OSMID/OBJECTID; NroLote/IdManzana/IdOrigen; UV_MZA/UV/MZA) | **APROBADO** |

## 6. Estados del panel

| # | Verificación | Resultado |
|---|---|---|
| 28 | Éxito: «N elementos encontrados» (singular/plural correcto) | **APROBADO** — `1 elemento encontrado`, `5 elementos encontrados` |
| 29 | Vacío (punto sin entidades, `-60.8000, -16.6000`): «No se encontraron elementos en este punto.» con lista vacía | **APROBADO** |
| 30 | Coordenadas del clic mostradas con 6 decimales (p. ej. `Latitud: -16.376775 · Longitud: -60.956268`) | **APROBADO** |

## 7. Coordenadas enviadas

| # | Verificación | Resultado |
|---|---|---|
| 31 | Orden correcto `lng` y luego `lat` en la query | **APROBADO** — `?lng=-60.95607...&lat=-16.37689...&tolerancia=10` |
| 32 | `tolerancia=10` fija en la petición | **APROBADO** |
| 33 | Se usan las coordenadas del clic (no las de `properties`) | **APROBADO** |

## 8. Clics rápidos (carrera)

| # | Verificación | Resultado |
|---|---|---|
| 34 | Tres clics rápidos: solo la **última** respuesta se pinta | **APROBADO** — el panel mostró las coordenadas y grupos del tercer clic |

## 9. Regresión FE-SIG 3 y ciclo normal

| # | Verificación | Resultado |
|---|---|---|
| 35 | Modo normal: clic sobre un Código Fijo abre el popup local con los datos FE-SIG 3 (`Código SIG 70.250.001`, `ID interno 1240`) | **APROBADO** |
| 36 | Modo normal: 0 peticiones `/identificar` | **APROBADO** |
| 37 | Pan: sigue recargando `bbox` de las capas activas (una petición por capa activa) | **APROBADO** — observado `manzanas/lotes/codigosfijos/vias/geojson` tras mover |
| 38 | Zoom: sigue funcionando (se usó `setView` real y recargó la vista) | **APROBADO** |
| 39 | Checkbox de capas: activar Lotes con su control actualizó la capa y el filtro | **APROBADO** |
| 40 | Leyenda: sigue visible y coherente con las capas activas | **APROBADO** |

## 10. Seguridad y red

| # | Verificación | Resultado |
|---|---|---|
| 41 | La petición `/identificar` lleva cabecera `Authorization` (Bearer) | **APROBADO** — observada en DevTools |
| 42 | CORS: `access-control-allow-origin: http://localhost:5000` | **APROBADO** |
| 43 | Todas las respuestas `/identificar` → `200` | **APROBADO** |
| 44 | `401` de identify conserva el flujo de sesión expirada de FE-AUTH | **APROBADO** — ver sección 14.1 (token invalidado en el navegador → `401` → limpieza de sesión → redirección a Login) |
| 45 | Logout sin regresión | **APROBADO** — ver sección 14.2 (logout por UI real: `sessionStorage` limpio y redirección esperada) |
| 46 | OpenStreetMap sigue funcionando | **APROBADO** — ver sección 14.3 (tiles `200`, pan y zoom cargan tiles, capas SIG encima) |

## 11. Consola

| # | Verificación | Resultado |
|---|---|---|
| 47 | Sin errores JavaScript propios de FE-SIG 4 | **APROBADO** con salvedad — el único mensaje es el `404` de `favicon.ico` (conocido y ajeno a FE-SIG 4) |

## 12. Responsive

Medido con render de la app real a cada ancho:

| # | Ancho | `innerWidth` | `scrollWidth` | Overflow | Resultado |
|---|---|---|---|---|---|
| 48 | 1366 px | 1366 | 1346 | No | **APROBADO** — panel 283, botón dentro, mapa 896×608 |
| 49 | 768 px | 768 | 748 | No | **APROBADO** — panel 679, botón dentro, mapa 716×440 |
| 50 | 360 px | 360 | 340 | No | **APROBADO** — panel 271, botón dentro, mapa 308×420 |
| 51 | Resultados con muchos elementos: desplazables (`scrollHeight > clientHeight` con `max-height: 340px`) | **APROBADO** |

## 13. Build y git

| # | Verificación | Resultado |
|---|---|---|
| 52 | `dotnet build VisorDatosSIG.sln` | **APROBADO** — 0 errores, 0 advertencias |
| 53 | `git diff --check` | **APROBADO** — limpio |

## 14. Cierre final FE-SIG 4 — verificación en navegador

Prueba real desde la Web (`http://localhost:5000`, rama `dev-josias`), con la API
real en `http://localhost:5080`. Navegador con DevTools: se inspeccionaron las
peticiones de red reales, el estado de `sessionStorage` y los *listeners* de Leaflet.

### 14.1 Prueba `401` en Identify (flujo FE-AUTH)

Procedimiento: inicio de sesión normal → `/Visor` → activar **Identificar** →
invalidar **solo** el `accessToken` de `sessionStorage` (usuario y `expiresAt`
intactos) → clic real sobre el mapa.

| # | Verificación | Resultado real |
|---|---|---|
| 54 | Se emite `GET /api/capas/identificar?...&tolerancia=10` | **APROBADO** — `GET /api/capas/identificar?lng=-60.96347808837891&lat=-16.381057942748143&tolerancia=10` |
| 55 | La API responde `401 Unauthorized` | **APROBADO** — `401`, con `www-authenticate: Bearer error="invalid_token"` |
| 56 | La petición lleva `Authorization: Bearer` (token invalidado) | **APROBADO** — cabecera `authorization` presente en la petición |
| 57 | `api.js` limpia la sesión según su comportamiento existente | **APROBADO** — `api.js` dispara `notificarSesionExpirada()` → `sesion.limpiar()` (no se modificó `api.js`) |
| 58 | Redirección al Login | **APROBADO** — `GET /Cuenta/Login?returnUrl=%2FVisor&motivo=expirada` (`200`) |
| 59 | `identify.js` **no** pisa ese flujo con mensajes ni redirecciones propias | **APROBADO** — la rama `status === 401` de `aplicarRespuesta` retorna sin fijar estado; el panel no mostró mensaje de error propio |

Observación: el `401` no se originó por un `identify.js` propio de error; el
despachador único delegó el clic y `api.js`/`guard.js` resolvieron la sesión
expirada como en FE-AUTH. Tras la prueba se inició sesión de nuevo para continuar.

### 14.2 Regresión Logout (UI real)

| # | Verificación | Resultado real |
|---|---|---|
| 60 | Con sesión válida, `/Visor` carga sus capas | **APROBADO** — Manzanas 863, Códigos Fijos 6.261, Vías 578 visibles |
| 61 | Logout desde la UI real (`Cerrar sesión`) | **APROBADO** — botón real de la barra |
| 62 | `sessionStorage` limpiado | **APROBADO** — `visorSIG.sesion` = `null`, `estaAutenticado() === false` |
| 63 | Redirección esperada | **APROBADO** — `/Cuenta/Login?motivo=sesion-finalizada` con aviso «Ha cerrado sesión correctamente.» |
| 64 | Volver a `/Visor` sin sesión redirige al Login | **APROBADO** — `/Cuenta/Login?returnUrl=%2FVisor` |

### 14.3 Regresión OpenStreetMap

| # | Verificación | Resultado real |
|---|---|---|
| 65 | Mapa base visible | **APROBADO** — plantilla `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`, 30 tiles cargados (zoom 13) |
| 66 | Al menos un tile OSM solicitado con `200` | **APROBADO** — tiles `a/b/c.tile.openstreetmap.org` con `200`/`304` |
| 67 | Pan sigue cargando tiles | **APROBADO** — tras desplazar la vista se solicitaron tiles nuevos (recursos OSM 79 → 86) |
| 68 | Zoom sigue cargando tiles | **APROBADO** — control real *Zoom in*: zoom 13 → 14, +35 tiles nuevos (`z=14/...`) |
| 69 | Las capas SIG se dibujan encima del mapa base | **APROBADO** — `leaflet-overlay-pane` (canvas) sobre `leaflet-tile-pane`; manzanas/vías visibles sobre OSM |
| 70 | No se modificó `map.js` | **APROBADO** — sin cambios en el diff de FE-SIG 4 |

### 14.4 Revisión funcional final (modos de clic)

| # | Verificación | Resultado real |
|---|---|---|
| 71 | Modo normal: clic → popup local FE-SIG 3 | **APROBADO** — popup local con datos (`UV_MZA UV 11 MZ 37`, `IdOrigen —`, `ID interno 27`) |
| 72 | Modo normal: 0 peticiones `/identificar` | **APROBADO** — `identificarTotal === 0` tras el clic |
| 73 | Modo Identificar: clic → exactamente 1 petición `/identificar` | **APROBADO** — 1 nueva: `GET /api/capas/identificar?lng=-60.970430374145515&lat=-16.394246695378072&tolerancia=10` → `200` |
| 74 | Modo Identificar: panel de identificación | **APROBADO** — panel visible, «1 elemento encontrado», coordenadas `-16.394247 · -60.970430` |
| 75 | Modo Identificar: sin popup local simultáneo | **APROBADO** — 0 `.leaflet-popup` tras el clic |
| 76 | Un solo *listener* de clic | **APROBADO** — `map._events.click.length === 1`, handler `despacharClic` |
| 77 | No existe un segundo *listener* de clic | **APROBADO** — el único handler es `despacharClic`; `identify.js` no registra ninguno |

## Notas

- **Capturas:** las capturas de pantalla se conservaron **fuera del repositorio**
  (`%TEMP%\opencode\fe-sig4_visor_1366.png` y `fe-sig4_visor_360.png`); no se creó
  registro de evidencias versionado para esta fase.
- **Método de clic:** los clics se despacharon como eventos de ratón reales
  (`mousedown`/`mouseup`/`click`) sobre el contenedor del mapa en el píxel de la
  geometría; nunca se llamó `openPopup()` ni se invocó `manejarClic` a mano. La
  delegación desde el despachador único se verificó con esos clics.
- **Precisión de píxel:** los escenarios que dependen de una geometría puntual
  (Código Fijo) se ejecutaron con el mapa en zoom alto para eliminar el error de
  redondeo del píxel del clic sintético.

## Limitaciones

- Tolerancia fija en 10 m.
- No se re-identifica automáticamente al cambiar capas (nota fija en el panel).
- No se dibujan ni resaltan geometrías identificadas.
- Los subcasos 44–46, antes pendientes, se ejecutaron y aprobaron en el cierre
  final (secciones 14.1–14.3).
