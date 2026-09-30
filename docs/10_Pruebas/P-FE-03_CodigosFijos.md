# P-FE-03 — Códigos Fijos reales en el visor

- **Fase:** FE-SIG 2
- **Módulo:** `src/VisorDatosSIG.Web`
- **Tipo:** prueba manual de funcionamiento end-to-end
- **Estado:** **APROBADO** — verificado manualmente por el responsable sobre
  `http://localhost:5000` contra la API real en `http://localhost:5080`.

> Solo se declara como **APROBADO** lo que está respaldado por las mediciones
> observadas. Lo que no se ejecutó se etiqueta explícitamente.

## Precondiciones
- Rama `dev-josias`.
- `dotnet build VisorDatosSIG.sln` correcto (0 errores, 0 advertencias).
- **Web:** `http://localhost:5000`.
- **API:** `http://localhost:5080` (proceso dejado en ejecución durante la
  prueba; solo se detuvo y reinició `VisorDatosSIG.Web` para liberar la salida).
- **SQL Server** con `dbo.CodigosFijos`: 6261 registros, `GeometriasNull = 0`,
  `SridIncorrecto = 0`, SRID 4326.
- Usuario de desarrollo `admin`; la contraseña no se documenta.
- Conexión a Internet (mapa base de OpenStreetMap).

## Pasos
1. Iniciar sesión en `/Cuenta/Login`.
2. Abrir `/Visor`.
3. Comprobar `GET /api/autenticacion/me`.
4. Comprobar la extensión y el GeoJSON de Códigos Fijos.
5. Comprobar el ajuste inicial y el conteo visible.
6. Cambiar el zoom y comprobar el conteo.
7. Desplazar el mapa y comprobar la nueva petición por *bbox*.
8. Repetir desplazamientos rápidos y comprobar que no se acumulan duplicados.
9. Abrir el popup de la primera *feature*.
10. Desactivar y reactivar la capa con el *checkbox*.
11. Comprobar la leyenda.
12. Probar la API sin JWT y con token inválido.
13. Cerrar sesión y abrir `/Visor` sin sesión.
14. Revisar la consola del navegador.
15. Repetir la visualización en 1366 / 768 / 360 px.

## Resultado esperado
- La capa muestra exclusivamente datos reales de la API, sin mocks.
- El conteo visible coincide con las *features* recibidas.
- El zoom y el desplazamiento ajustan el *bbox* y el conteo.
- No se acumulan duplicados.
- El popup muestra datos reales y `NULL` como `—`.
- El control de activación oculta y recarga la capa.
- La API rechaza las peticiones sin JWT válido.
- No aparecen errores JavaScript en consola.
- La interfaz es utilizable a 1366 / 768 / 360 px.

## Resultado obtenido

### A. Sesión y arranque

| # | Verificación | Resultado |
|---|---|---|
| 1 | Login correcto y acceso al visor | **APROBADO** |
| 2 | `/Visor` carga correctamente | **APROBADO** |
| 3 | `GET /api/autenticacion/me` → 200 | **APROBADO** |
| 4 | `GET /api/capas/extension?capa=codigosfijos` → 200 | **APROBADO** |
| 5 | `GET /api/capas/codigosfijos/geojson?...&limit=10000` → 200 | **APROBADO** |
| 6 | Respuesta es una `FeatureCollection` real (6261 registros de SQL Server) | **APROBADO** |
| 7 | Sin mocks, sin JSON local y sin SQL desde JavaScript | **APROBADO** |

### B. Representación de los puntos

| # | Verificación | Resultado |
|---|---|---|
| 8 | Puntos dibujados sobre San Ignacio de Velasco | **APROBADO** |
| 9 | Ajuste inicial de la vista con la extensión devuelta por la API | **APROBADO** |
| 10 | Conteo visible igual a las *features* recibidas | **APROBADO** |
| 11 | 6261 puntos en la extensión inicial | **APROBADO** |
| 12 | El zoom cambia el conteo (zoom 17 → 313; zoom 15 → 5118) | **APROBADO** |
| 13 | El desplazamiento genera una petición por el nuevo *bbox* | **APROBADO** |
| 14 | Sin puntos duplicados tras 6 desplazamientos rápidos (estado final: 3521, coincidente con el *bbox* final) | **APROBADO** |
| 15 | Popup con datos reales (`Código SIG` 30.445.002, `Nombre` AGUILAR SOCORE AGUSTIN EUGENIO, `Estado` Normal, `ID interno` 1) | **APROBADO** |
| 16 | Valor `NULL` renderizado como `—` (campo Lote) | **APROBADO** |

### C. Control de capa y leyenda

| # | Verificación | Resultado |
|---|---|---|
| 17 | El *checkbox* oculta los puntos (capa removida, `hasLayer` false, `estaActiva()` false, *badge* "Inactiva", conteo vacío) | **APROBADO** |
| 18 | Con la capa desactivada, `moveend` **no** genera petición GeoJSON | **APROBADO** |
| 19 | La reactivación recarga la capa (6261 puntos para el *bbox* actual) | **APROBADO** |
| 20 | La leyenda aparece al activar y se oculta al desactivar | **APROBADO** |

### D. Seguridad y sesión

| # | Verificación | Resultado |
|---|---|---|
| 21 | `GET /api/capas/extension?capa=codigosfijos` sin token → 401 | **APROBADO** |
| 22 | `GET /api/capas/codigosfijos/geojson?...` sin token → 401 | **APROBADO** |
| 23 | Extensión con token inválido `Bearer token.invalido.xyz` → 401 | **APROBADO** |
| 24 | Logout: navegación a `/Cuenta/Login?motivo=sesion-finalizada`, `sessionStorage` (`visorSIG.sesion`) limpiado, cabecera anónima y formulario presentes | **APROBADO** |
| 25 | Sin sesión, `/Visor` redirige a `/Cuenta/Login?returnUrl=%2FVisor` | **APROBADO** |
| 26 | Consola del navegador sin errores ni advertencias | **APROBADO** |

> La **expiración natural del JWT** no se volvió a medir en esta fase; sigue
> registrada como **PENDIENTE DE EJECUCIÓN** en `P-FE-02`. Lo verificado aquí es
> la redirección con sesión ausente o ya finalizada.

> **OpenStreetMap:** el mapa base de OSM se observó **renderizado** en la captura
> del visor: se distinguen las etiquetas de San Ignacio de Velasco, la red vial,
> el escudo de la ruta F10 y el sombreado del terreno, además del enlace de
> atribución de Leaflet y OpenStreetMap. No se registró una medición de red
> explícita de los *tiles*.

### E. Responsive

| # | Ancho | Resultado |
|---|---|---|
| 27 | 1366 px | **APROBADO** — sin desbordamiento (`scrollWidth` 1366 = `innerWidth`); dos columnas "320px 896px" (panel 320, mapa 896x608); *checkbox* dentro del *viewport*; leyenda a una columna |
| 28 | 768 px | **APROBADO** — sin desbordamiento (768 = 768); una columna de 736px; mapa 736x440; *checkbox* y *badge* dentro del *viewport*; leyenda a 2 columnas |
| 29 | 360 px | **APROBADO** — sin desbordamiento (`scrollWidth` de `document` y `body` = 360); una columna; mapa 328x420; *checkbox* 16x16 dentro del *viewport*; *badge* dentro del *viewport*; leyenda a 2 columnas; ítems del panel con salto de línea |

### F. Build

| # | Verificación | Resultado |
|---|---|---|
| 30 | `dotnet build VisorDatosSIG.sln` | **APROBADO** — 0 errores, 0 advertencias |

### G. Reverificación final del popup (tras la corrección de maquetación)

Ejecutada **en el visor real, con datos reales**: clic sobre un punto del lienzo
Leaflet. Contexto de la corrida: la vista del mapa ya se había movido y
zoomeado durante la sesión, por lo que el conteo visible en ese momento era
5537 (la carga inicial, con la extensión completa, había dado 6261).

| # | Verificación | Resultado |
|---|---|---|
| 31 | Clic sobre un punto abre el popup | **APROBADO** — el evento se despachó sobre el `CANVAS` de Leaflet y el popup abierto pertenece al marcador de la *feature* `id = 3` (`isPopupOpen()` true) |
| 32 | Seis filas con claves y valores | **APROBADO** — `Código SIG` 30.440.002, `Código fijo` 6585, `Nombre` TOMICHA LANDIVAR FELIPE, `Estado` Normal, `Lote` —, `ID interno` 3 |
| 33 | Los valores coinciden con la *feature* real de la API | **APROBADO** — las seis filas comparadas una a una contra `feature.properties` y `feature.id`: todas coinciden |
| 34 | Ninguna clave cortada | **APROBADO** — las seis claves con una sola línea |
| 35 | Claves y valores alineados entre filas | **APROBADO** — todas las claves en la misma `x`, todos los valores en la misma `x`, cada pareja en su propia fila |
| 36 | Sin *overflow* | **APROBADO** — `scrollWidth` = `clientWidth`; popup de 221 px dentro del *viewport* |
| 37 | `NULL` de Lote como `—` | **APROBADO** — `IdLote` es `NULL` en la API y se muestra `—` |
| 38 | Consola tras la interacción | **APROBADO** — sin mensajes |

## Notas
- **Discrepancia de `minY`:** el enunciado previo de la tarea indicaba
  `minY = -16.449702244308323`, pero la API devuelve realmente
  `minY = -16.440702244308323` (diferencia en el cuarto decimal). Se registra el
  valor real observado. La extensión no está fija en el frontend: se obtiene de
  la API.
- **SHP vs SQL:** el SHP original fue diagnosticado con **6271 registros**,
  mientras que SQL Server contiene **6261**. La causa de los 10 registros de
  diferencia **no se conoce** y **no se inventa**; queda pendiente contrastarla
  contra el reporte y la bitácora del Migrador.
- **Evidencias:** las capturas de pantalla se tomaron pero se conservaron
  **fuera del repositorio**; no se creó un registro de evidencias para esta fase.
- **Corrección posterior del popup (solo presentación):** tras el recorrido
  end-to-end se detectó que la rejilla de dos columnas estaba declarada sobre el
  `dl`, de modo que cada fila (que envuelve su clave y su valor) ocupaba una sola
  celda y las parejas se repartían alternando columnas. Se corrigió en `visor.css`
  moviendo la rejilla a la lista y disolviendo la fila con `display: contents`.
  La verificación de los **datos** del popup es la del recorrido real (fila 15).
  La corrección de **maquetación** se reverificó después **en el visor real con
  datos reales** (sección G, filas 31 a 38): clic sobre un punto del lienzo,
  popup de seis filas, claves y valores alineados, ninguna clave cortada, sin
  *overflow*, `NULL` como `—` y consola limpia. No afecta a ninguna otra medición.

## Incidencias
Ninguna durante el flujo end-to-end. La única observación es la diferencia de
`minY` respecto del enunciado, anotada arriba.

## Limitaciones
- Solo está implementada la capa de Códigos Fijos. Manzanas, Lotes y Vías
  siguen pendientes.
- `identify.js` continúa siendo un *placeholder*: `/api/capas/identificar` no se
  consume en esta fase.
- La respuesta GeoJSON está limitada por el `limit` 10000; por encima de ese
  valor el servidor trunca y el frontend no pagina (con 6261 registros el límite
  no se alcanza hoy).
- No hay identificación espacial, ni filtros por atributos, ni exportación.
- La expiración natural del JWT sigue pendiente de ejecución (`P-FE-02`).
- Queda pendiente adjuntar las capturas de evidencia al repositorio.
