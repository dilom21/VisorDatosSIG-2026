# P-FE-01 — El mapa Leaflet y el mapa base se visualizan en `/Visor`

- **Fase:** FE-SIG 1
- **Módulo:** `src/VisorDatosSIG.Web`
- **Tipo:** prueba manual de funcionamiento
- **Estado:** **APROBADO** — ejecutado manualmente por el responsable (Rol 4) en navegador sobre `http://localhost:5000/Visor`

## Precondiciones
- Rama `dev-josias`.
- `dotnet build VisorDatosSIG.sln` correcto.
- Ejecutar: `dotnet run --project src/VisorDatosSIG.Web/VisorDatosSIG.Web.csproj`
- Conexión a Internet (el mapa base es OpenStreetMap online).

## Pasos
1. Abrir `/Visor`.
2. Esperar la carga del mapa base.
3. Hacer zoom (botones `+`/`-` y rueda del ratón).
4. Desplazar el mapa (arrastrar).
5. Abrir la consola de desarrollador del navegador (F12).
6. Repetir la visualización en ancho aproximado de 1366 px, 768 px y 360 px.

## Resultado esperado
- El mapa Leaflet se visualiza correctamente.
- OpenStreetMap carga (tiles visibles).
- Zoom y desplazamiento funcionan.
- No aparecen errores JavaScript.
- El contenedor del mapa nunca queda con altura cero.
- El mapa sigue siendo utilizable a 1366 / 768 / 360 px.

## Resultado obtenido
**APROBADO** (ejecución manual real por el responsable del Rol 4):

| Verificación | Resultado |
|---|---|
| `/Visor` carga correctamente | OK |
| Leaflet visible | OK |
| OpenStreetMap visible | OK |
| Zoom con botones y rueda | Correcto |
| Desplazamiento del mapa (arrastrar) | Correcto |
| Atribución OSM/Leaflet visible | Sí |
| `window.VisorSIG.mapa.estaInicializado()` | Devuelve `true` |
| `window.VisorSIG.mapa.obtener()` | Devuelve la instancia del mapa |
| Inicialización repetida | No crea otro mapa ni produce "Map container is already initialized" |
| Consola JavaScript | Sin errores relevantes |
| 1366 px | Correcto |
| 768 px | Correcto |
| 360 px | Correcto |
| Recarga del navegador | Correcta |

Verificación en servidor (complementaria, ejecutada desde el entorno de desarrollo):
- `/` → 200; `/Visor` → 200.
- `/lib/leaflet/leaflet.css` → 200, `/lib/leaflet/leaflet.js` → 200.
- `/lib/leaflet/images/marker-icon.png`, `marker-icon-2x.png`, `marker-shadow.png` → 200.
- `/js/visor/map.js` → 200.

## Evidencia
Registrada en `docs/11_Evidencias/FE-SIG-1_Registro_Evidencias.md`.

- Evidencia visual principal: mapa base Leaflet/OpenStreetMap funcionando en `/Visor`
  (observada durante la ejecución manual). **Archivo de imagen pendiente de adjuntar**
  en `docs/11_Evidencias/01_mapa_base.png`.
- Evidencia de comportamiento (consola del navegador): `estaInicializado()` → `true`,
  `obtener()` → instancia del mapa, sin errores relevantes, sin doble inicialización.

## Incidencias
Ninguna registrada hasta el momento.

## Limitaciones
- El mapa base requiere Internet (tiles OpenStreetMap online).
- No hay capas ni datos cargados en esta fase.
- Queda pendiente adjuntar el archivo de imagen de evidencia visual.
