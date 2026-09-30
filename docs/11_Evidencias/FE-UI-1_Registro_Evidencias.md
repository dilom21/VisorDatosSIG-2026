# Registro de evidencias — FE-UI 1 (Identidad visual base)

- **Fase:** FE-UI 1
- **Rama:** `dev-josias`
- **Documento de fase:** [`docs/09_VisorSIG/FE-UI-1_Identidad_Visual.md`](../09_VisorSIG/FE-UI-1_Identidad_Visual.md)
- **Caso de prueba asociado:** `P-FE-02` (complementa el flujo de Login ya aprobado)
- **Entorno:**
  - Web: `http://localhost:5000` (`dotnet run --project src/VisorDatosSIG.Web/VisorDatosSIG.Web.csproj`)
  - API: `http://localhost:5080`, ejecutada desde su propio worktree
  - SQL Server en `localhost`, base de datos `VisorDatosSIG`
  - Navegador: Brave (motor Chromium), zoom de página al 75 % durante la captura
  - Usuario de desarrollo: `admin` (contraseña **no** documentada)

## Capturas versionadas

Todas las imágenes listadas existen realmente en el repositorio y fueron
inspeccionadas una por una para confirmar que corresponden a la vista que su
nombre declara.

| Archivo | Vista | Resolución real | Estado del visor |
|---|---|---|---|
| `04_home_ui_1366.png` | Inicio — escritorio | 1366 × 1000 px | Anónimo (botón "Iniciar sesión") |
| `05_login_ui_1366.png` | Login — escritorio | 1366 × 1000 px | Anónimo |
| `06_visor_ui_1366.png` | Visor — escritorio | 1025 × 900 px | **Autenticado** (`Administrador`) |
| `07_home_ui_360.png` | Inicio — móvil | 270 × 820 px | **Autenticado** (`Administrador`) |
| `08_login_ui_360.png` | Login — móvil | 270 × 820 px | **Autenticado** |
| `09_visor_ui_360.png` | Visor — móvil | 270 × 820 px | **Autenticado** (`Administrador`) |

Ubicación: `docs/11_Evidencias/FrontendSIG/`.

### Sobre las dimensiones de las capturas

- `04`, `05` y `06` son capturas de escritorio. `06` mide 1025 px de ancho en
  lugar de 1366 porque el navegador tenía **zoom al 75 %**: 1366 × 0,75 = 1024,5.
  El layout capturado corresponde al viewport CSS de **1366 px**, que es lo que
  se quería evidenciar.
- `07`, `08` y `09` corresponden al viewport CSS de **360 px**. Por el mismo
  zoom al 75 % (360 × 0,75 = 270) el archivo mide 270 px de ancho. El layout
  capturado es el de 360 px, y cada imagen muestra la página completa sin
  recortes ni desplazamiento horizontal.

## Capturas descartadas (no versionadas)

Se descartaron de forma explícita para no incorporar evidencia defectuosa o
redundante:

| Archivo de origen | Motivo del descarte |
|---|---|
| `ui2_home_360.png` | **Defectuosa**: es un recorte de 360 px del layout; el contenido aparece cortado en el borde derecho (texto del hero y bordes de las tarjetas), lo que se leería como un desbordamiento inexistente. Se usó en su lugar `ui2_home_360_mcp.png`. |
| `ui2_login_360.png` | **Defectuosa**: mismo recorte; la tarjeta de login, el campo de contraseña y el botón "Mostrar" quedan cortados en el borde derecho. Se usó en su lugar `ui2_login_360_mcp.png`. |
| `ui2_home_768.png` | Vista de 768 px, fuera de la nomenclatura solicitada para esta fase. |
| `ui2_login_768.png` | Vista de 768 px, fuera de la nomenclatura solicitada. |
| `ui2_visor_768.png` | Vista de 768 px, fuera de la nomenclatura solicitada. |

## Evidencia observada (ejecución manual real, contra la API activa)

### Páginas e interfaz

- `/`, `/Cuenta/Login` y `/Visor` responden **200** y se renderizan con la nueva identidad.
- **Inicio**: hero con la descripción real del sistema y tarjetas de las capas
  contempladas (Manzanas, Lotes, Códigos Fijos, Vías), cada una con la etiqueta
  "Prevista" y la nota de que aún no están disponibles en la Web.
- **Login**: tarjeta de dos paneles; el panel de identidad conserva el `h1`
  "VisorDatosSIG" también en móvil.
- **Visor**: panel lateral "Capas" y mapa a ancho completo, con la cabecera en
  estado autenticado.

### Login real validado

- Credenciales **incorrectas** → `POST http://localhost:5080/api/autenticacion/iniciar`
  → **401**; la interfaz muestra "Usuario o contraseña incorrectos." con el estilo
  de alerta de error; exactamente una petición; el botón se restaura.
- Credenciales **válidas** del entorno de desarrollo → **200**, sesión guardada y
  redirección a `/Visor`.
- `sessionStorage` contiene exactamente `accessToken`, `usuario` y `expiresAt`.
- El guard ejecuta `GET /api/autenticacion/me`, la página sale del estado
  `sesion-verificando` y el mapa queda operativo.
- **Logout** → `POST .../cerrar`, redirección a
  `/Cuenta/Login?motivo=sesion-finalizada`, `sessionStorage` limpio y mensaje
  "Ha cerrado sesión correctamente."; la cabecera vuelve al estado anónimo.

### Leaflet validado

- El mapa renderiza el mapa base de OpenStreetMap (se observa San Ignacio de
  Velasco) con controles de zoom y la atribución "Leaflet | © OpenStreetMap contributors".
- Se cargaron entre 20 y 25 tiles correctamente en la verificación de escritorio.
- La inicialización no se alteró: no se modificó ningún archivo de `js/visor/`.

### Consola sin errores

- Durante el flujo completo (Inicio → Login → Visor → Logout) la consola del
  navegador no registró **ningún** mensaje: ni errores, ni advertencias, ni
  aserciones.

### Responsive 1366 / 768 / 360

Medición con el viewport CSS real sobre las tres rutas:

| Ancho | Inicio | Login | Visor | Desbordamiento horizontal |
|---|---|---|---|---|
| **1366 px** | 4 tarjetas de capas (299 px) en una fila; contenedor 1280 px; cabecera 64 px | Tarjeta 880 px con panel lateral + formulario | Panel 320 px + mapa 896 × 840 px | **No** |
| **768 px** | 3 tarjetas de capas (237 px) | Tarjeta 520 px, panel de identidad en franja compacta | Una columna: panel y mapa a 736 px; mapa 440 px de alto | **No** |
| **360 px** | 1 tarjeta por fila (308 px) | Tarjeta 328 px, franja compacta con el `h1` conservado | Una columna: panel y mapa a 308 px; mapa 420 px de alto | **No** |

La verificación de 768 px es **por medición**, no hay captura versionada para ese
ancho en esta fase.

> Nota: la verificación a 360 px se hizo con emulación de viewport, porque el modo
> `--headless` de Edge en Windows no respeta anchos inferiores a ~420 px y recorta
> la imagen. Las capturas versionadas `07`, `08` y `09` son la evidencia de ese ancho.

### Accesibilidad aplicada

- Enlace "Saltar al contenido" y `<main id="contenido">`.
- `:focus-visible` global con contorno de acento de 3 px, también en campos y botones.
- `aria-label` en la navegación principal y en el enlace de marca; SVG decorativos
  con `aria-hidden="true"` y `focusable="false"`.
- Conservados `role="alert"`, `role="status"`, `aria-pressed`, `aria-controls`,
  `aria-busy` y todos los `<label for>`.
- Navegación con `<a>` reales y acciones con `<button>` reales.
- `prefers-reduced-motion` reduce las transiciones y detiene el indicador de carga.

### Servidor y compilación

| Recurso | Resultado |
|---|---|
| `/` | 200 |
| `/Cuenta/Login` | 200 |
| `/Visor` | 200 |
| `/css/visor.css` | 200 |
| `/css/auth.css` | 200 |
| `dotnet build VisorDatosSIG.sln` | Compilación correcta: 0 advertencias, 0 errores |

## Casos no cubiertos por esta evidencia

- Responsive en orientación horizontal de móvil.
- Lectores de pantalla reales (NVDA/JAWS): la accesibilidad se validó por
  estructura y atributos, no con auditoría asistida.
- Visualización en navegadores distintos de Chromium (Firefox, Safari).

## Incidencias

- Ninguna incidencia funcional. Durante la primera verificación visual el
  navegador sirvió `visor.css` desde caché (ETag sin revalidar) y fue necesario
  forzar una recarga sin caché para tomar el CSS actualizado.
- Se detectó y corrigió, dentro de la fase, un desbordamiento del grid de capas en
  pantallas estrechas (`minmax(200px, 1fr)` → `minmax(min(220px, 100%), 1fr)`).

> No se documentan el token JWT real, la clave de firma, los hashes o salts de
> contraseña, las cadenas de conexión ni la contraseña utilizada en la prueba.
