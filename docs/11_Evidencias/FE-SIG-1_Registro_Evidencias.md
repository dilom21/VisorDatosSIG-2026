# Registro de evidencias — FE-SIG 1 (Leaflet y mapa base)

- **Fase:** FE-SIG 1
- **Rama:** `dev-josias`
- **Caso de prueba asociado:** `P-FE-01` (APROBADO)
- **Entorno:** `dotnet run --project src/VisorDatosSIG.Web/VisorDatosSIG.Web.csproj` → `http://localhost:5000/Visor`

## Evidencia observada (ejecución manual real)

Ejecutada por el responsable del Rol 4 en navegador. Observaciones registradas:

- `/Visor` carga correctamente.
- Leaflet visible.
- OpenStreetMap visible.
- Zoom con botones y rueda: correcto.
- Desplazamiento del mapa: correcto.
- Atribución OSM/Leaflet visible.
- `window.VisorSIG.mapa.estaInicializado()` → `true`.
- `window.VisorSIG.mapa.obtener()` → instancia del mapa.
- Inicialización repetida: no crea otro mapa ni produce "Map container is already initialized".
- Consola JavaScript sin errores relevantes.
- Responsive: 1366 px correcto, 768 px correcto, 360 px correcto.
- Recarga del navegador: correcta.

## Archivos de evidencia

| Archivo esperado | Estado |
|---|---|
| `docs/11_Evidencias/01_mapa_base.png` | **PENDIENTE DE ADJUNTAR** (evidencia visual principal observada, archivo de imagen aún no incorporado al repositorio) |
| `docs/11_Evidencias/10_responsive_1366.png` | Pendiente |
| `docs/11_Evidencias/11_responsive_768.png` | Pendiente |
| `docs/11_Evidencias/12_responsive_360.png` | Pendiente |

> Nota: no se declara la existencia de capturas que no estén realmente en el
> repositorio. Cuando se adjunten, actualizar esta tabla.

## Evidencia en servidor (complementaria)

| Recurso | Resultado |
|---|---|
| `/` | 200 |
| `/Visor` | 200 |
| `/lib/leaflet/leaflet.css` | 200, text/css, 14806 bytes |
| `/lib/leaflet/leaflet.js` | 200, text/javascript, 147563 bytes |
| `/lib/leaflet/images/marker-icon.png` | 200, image/png |
| `/lib/leaflet/images/marker-icon-2x.png` | 200, image/png |
| `/lib/leaflet/images/marker-shadow.png` | 200, image/png |
| `/js/visor/map.js` | 200, text/javascript, 2843 bytes |

## Incidencias

Ninguna.
