# FE-SIG 1 — Integración de Leaflet y mapa base

## Objetivo
Integrar Leaflet de forma local en el proyecto Web e inicializar el mapa con un
mapa base de OpenStreetMap, dentro de la vista del visor `/Visor`.

Alcance estricto de esta fase: **sin capas, sin datos, sin GeoJSON, sin fetch,
sin API, sin mocks, sin control de capas, sin leyenda, sin identificación.**

## Tecnología
- ASP.NET Core MVC / Razor (`VisorDatosSIG.Web`)
- Leaflet (biblioteca JavaScript de mapas)

## Versión de Leaflet
- **Leaflet 1.9.4** (`Leaflet 1.9.4+v1.d15112c`)
- Distribución oficial descargada desde el release de GitHub
  (`Leaflet/Leaflet` v1.9.4). No se usa CDN.

## Ubicación local de los assets
```
src/VisorDatosSIG.Web/wwwroot/lib/leaflet/
├── leaflet.css        (14.806 bytes)
├── leaflet.js         (147.563 bytes)
└── images/
    ├── marker-icon.png     (1.466 bytes)
    ├── marker-icon-2x.png  (2.464 bytes)
    └── marker-shadow.png     (618 bytes)
```
Solo se copiaron los archivos necesarios para el mapa base. La integridad se
verificó comparando el hash SHA-256 de cada archivo con el origen extraído
(resultado: idéntico en los 5 archivos).

## Centro inicial
```js
[-16.3811, -60.9636]   // Leaflet usa [latitud, longitud]
```
Valor únicamente de **vista inicial**; cuando existan capas reales el visor
podrá ajustar la vista con `bounds`/`fitBounds`.

## Zoom inicial
```
13
```

## Mapa base
OpenStreetMap mediante tiles **online**:
```
https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png
```
Atribución: `© OpenStreetMap contributors`. No se implementan tiles offline ni
servidor de mapas local en esta fase.

## Dependencia de Internet
> **El mapa base de OpenStreetMap requiere conexión a Internet.
> Leaflet está almacenado localmente en el proyecto.**

## Archivos creados
- `src/VisorDatosSIG.Web/wwwroot/lib/leaflet/leaflet.css`
- `src/VisorDatosSIG.Web/wwwroot/lib/leaflet/leaflet.js`
- `src/VisorDatosSIG.Web/wwwroot/lib/leaflet/images/marker-icon.png`
- `src/VisorDatosSIG.Web/wwwroot/lib/leaflet/images/marker-icon-2x.png`
- `src/VisorDatosSIG.Web/wwwroot/lib/leaflet/images/marker-shadow.png`
- `docs/09_VisorSIG/FE-SIG-1_Leaflet_MapaBase.md` (este documento)
- `docs/10_Pruebas/P-FE-01_MapaBase_Leaflet.md`

## Archivos modificados
- `src/VisorDatosSIG.Web/Views/Shared/_Layout.cshtml`
  - se agregó `@await RenderSectionAsync("Styles", required: false)` dentro de
    `<head>`, antes de `visor.css`, para que una vista pueda cargar estilos
    propios sin afectar a las demás.
- `src/VisorDatosSIG.Web/Views/Visor/Index.cshtml`
  - sección `Styles` que carga únicamente `~/lib/leaflet/leaflet.css`
    (solo en la vista del visor; la página Home no carga Leaflet);
  - `~/lib/leaflet/leaflet.js` cargado **antes** de los módulos del visor;
  - se eliminó el texto provisional del contenedor `#map`, requisito para que
    Leaflet inicialice el contenedor sin contenido previo.
- `src/VisorDatosSIG.Web/wwwroot/css/visor.css`
  - el contenedor `.visor-map` define una **altura explícita** (520 px; 420 px
    en ≤768 px) y un color de fondo, en lugar de solo `min-height`;
  - el `padding` se deja únicamente en `.visor-panel`, porque el padding sobre
    el contenedor del mapa desplaza los paneles de Leaflet;
  - responsive básico: una columna en ≤768 px.
- `src/VisorDatosSIG.Web/wwwroot/js/visor/map.js`
  - implementación real del módulo (antes: 1 línea de comentario).

## Detalle de `map.js`
- Expone la API en `window.VisorSIG.mapa`:
  `centroInicial`, `zoomInicial`, `urlTilesOsm`, `inicializar()`, `obtener()`,
  `obtenerCapaBase()`, `estaInicializado()`, `refrescarTamano()`.
- La instancia del mapa queda accesible de forma controlada mediante
  `VisorSIG.mapa.obtener()`, para que las fases siguientes registren capas sin
  volver a crear el mapa.
- **Inicialización idempotente**: el estado se guarda en el namespace
  (`ns._mapa`, `ns._capaBase`), de modo que una segunda ejecución del script
  reutiliza el mapa en lugar de crear otro sobre `#map`. Además hay guardas
  defensivas: si `L` no está disponible o el contenedor ya tiene `_leaflet_id`,
  no se crea un mapa nuevo.
- `refrescarTamano()` llama a `invalidateSize()` y se enlaza una sola vez al
  evento `resize`, para cuando cambie el layout.
- No contiene lógica SIG (capas, datos, identificación, leyenda).

## Prueba realizada
**Verificación en servidor (ejecutada):**
- `dotnet build VisorDatosSIG.sln` → compilación correcta, 0 warnings, 0 errores.
- `dotnet run --project src/VisorDatosSIG.Web/VisorDatosSIG.Web.csproj` →
  `Now listening on: http://localhost:5000`.
- Respuestas HTTP verificadas:

| Recurso | Resultado |
|---|---|
| `/` | 200 (sigue funcionando) |
| `/Visor` | 200, `text/html` |
| `/lib/leaflet/leaflet.css` | 200, `text/css`, 14806 bytes |
| `/lib/leaflet/leaflet.js` | 200, `text/javascript`, 147563 bytes |
| `/lib/leaflet/images/marker-icon.png` | 200, `image/png` |
| `/lib/leaflet/images/marker-icon-2x.png` | 200, `image/png` |
| `/lib/leaflet/images/marker-shadow.png` | 200, `image/png` |
| `/js/visor/map.js` | 200, `text/javascript`, 2843 bytes |

- El HTML de `/Visor` contiene `lib/leaflet/leaflet.css`, `lib/leaflet/leaflet.js`,
  `js/visor/map.js` y `id="map"`.

**Prueba visual y de consola del navegador: APROBADA** (ejecución manual real por
el responsable del Rol 4 sobre `http://localhost:5000/Visor`):
- mapa Leaflet y OpenStreetMap visibles; zoom (botones y rueda) y desplazamiento correctos;
- atribución OSM/Leaflet visible;
- `window.VisorSIG.mapa.estaInicializado()` → `true` y `window.VisorSIG.mapa.obtener()` → instancia;
- la inicialización repetida no crea otro mapa ni provoca "Map container is already initialized";
- consola JavaScript sin errores relevantes; recarga del navegador correcta;
- responsive verificado a 1366 / 768 / 360 px.

Queda registrada como caso `P-FE-01` en `docs/10_Pruebas/` (APROBADO).

## Resultado
- Integración local de Leaflet 1.9.4: **verificada** (build limpio y assets servidos con 200).
- Mapa base OSM visible, zoom, desplazamiento y consola sin errores relevantes: **APROBADO** (prueba manual).
- Responsive a 1366/768/360 px: **APROBADO** (prueba manual).
- Inicialización idempotente verificada en navegador: **APROBADO**.
- Caso de prueba `P-FE-01`: **APROBADO**.

## Limitaciones
- El mapa base requiere Internet (tiles OSM online).
- No hay capas ni datos todavía; el mapa se muestra vacío, solo con el mapa base.
- Queda pendiente adjuntar el archivo de imagen de evidencia visual en `docs/11_Evidencias/01_mapa_base.png`.
- No se implementaron tiles offline.

## Siguiente fase
**FE-SIG 2 — CodigosFijos**: consumo del GeoJSON real del backend y
representación de puntos, identificación y popup/panel, con estados de
loading/error. Pendiente de que el endpoint real exista; si aún no existe, se
requerirá autorización expresa para un mock temporal encapsulado.
