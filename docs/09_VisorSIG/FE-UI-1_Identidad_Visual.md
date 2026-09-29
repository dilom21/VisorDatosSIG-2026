# FE-UI 1 — Identidad visual base de VisorDatosSIG.Web

- **Fase:** FE-UI 1
- **Módulo:** `src/VisorDatosSIG.Web`
- **Rama:** `dev-josias`
- **Estado:** **IMPLEMENTADO Y VERIFICADO** (build limpio, páginas servidas, comportamiento
  y responsive medidos en navegador con la API real activa)

## Objetivo
Mejorar la identidad visual del sistema sin introducir frameworks frontend, sin
cambiar la arquitectura y sin alterar la funcionalidad SIG ni la autenticación.
El resultado debe percibirse como una aplicación universitaria/cartográfica
profesional, limpia, moderna y sobria.

Queda **fuera de alcance**: convertir la interfaz en una landing comercial,
agregar animaciones excesivas o añadir funcionalidades que no existen.

## Principios visuales
1. **Sobriedad cartográfica**: azul profundo como color principal y teal SIG como acento.
2. **Superficies limpias**: fondos gris muy claro y superficies blancas con bordes y
   sombras muy sutiles.
3. **Jerarquía clara**: encabezados con peso, texto secundario atenuado y acciones
   siempre reconocibles como botón.
4. **Espacio aprovechado**: contenido dentro de un ancho máximo legible; el visor
   ocupa todo el alto disponible.
5. **Movimiento mínimo**: solo transiciones de 120 ms en color y borde, desactivadas
   bajo `prefers-reduced-motion`.
6. **Sin dependencias externas**: cero frameworks, cero CDN, cero fuentes descargadas.

## Sistema de diseño
Todos los colores, radios, sombras y espaciados se centralizan como variables CSS
en `:root` dentro de `visor.css` y se consumen por `var(...)`; no se repiten
hexadecimales por el resto de las hojas.

| Grupo | Variables |
|---|---|
| Marca | `--color-primary`, `--color-primary-dark`, `--color-primary-soft`, `--color-accent`, `--color-accent-dark` |
| Superficies | `--color-background`, `--color-surface`, `--color-surface-alt` |
| Texto | `--color-text`, `--color-muted` |
| Bordes | `--color-border`, `--color-border-strong` |
| Estados | `--color-error(-bg/-border)`, `--color-warning(...)`, `--color-success(...)`, `--color-info(...)` |
| Formas | `--radius-sm`, `--radius-md`, `--radius-lg` |
| Elevación | `--shadow-sm`, `--shadow-md`, `--shadow-lg` |
| Espaciado | `--space-1` … `--space-7` |
| Layout | `--header-height` (64 px), `--container-width` (1280 px) |
| Movimiento | `--transition-fast` (120 ms) |

**Tipografía:** stack del sistema, sin descargas externas:
`Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`.

**Componentes reutilizables definidos como clases** en `visor.css`:
`skip-link`, `app-header`, `brand`, `app-nav`, `app-main`, `btn-primario`,
`btn-secundario`, `btn-sesion`, `btn-sesion--ghost`, y el sistema de alertas
(`login-mensaje` / `login-error` / `login-info` / `visor-aviso` / `alerta-exito`).

## Layout global
`_Layout.cshtml` ahora ofrece:
- Enlace "Saltar al contenido" para navegación por teclado.
- Cabecera de 64 px de alto, superficie blanca, borde inferior y sombra sutil,
  con el contenido centrado en un ancho máximo de 1280 px.
- **Marca** a la izquierda: símbolo cartográfico (pin de mapa) en SVG inline sobre
  un degradado azul→teal, con el nombre y el subtítulo "Información geográfica".
- **Navegación** con los dos enlaces existentes (`Inicio`, `Visor cartográfico`).
  No se agregaron rutas ni módulos inexistentes.
- **Sesión** a la derecha: botón "Iniciar sesión" en estado anónimo; en estado
  autenticado, un *chip* con icono de usuario, nombre, rol y botón "Cerrar sesión".

`_Layout.cshtml` conserva íntegros `#sesion-anonima`, `#sesion-activa`,
`#sesion-nombre`, `#sesion-rol` y `#btn-cerrar-sesion`, además de la inyección de
`Api:BaseUrl` con `@Json.Serialize` y el orden de carga de los scripts de `auth/`.

## Login
Composición de tarjeta amplia (~880 px) construida con dos paneles:
- **Panel de identidad**: degradado azul→teal, trama cartográfica abstracta en SVG
  inline (curvas de nivel, anillos y nodos), marca con el pin, `h1` "VisorDatosSIG"
  y la leyenda "Migración y consulta de información geográfica".
- **Panel del formulario**: "Bienvenido" + "Inicie sesión para continuar", campos
  Usuario y Contraseña de 46 px de alto, botón Mostrar/Ocultar con icono de ojo
  integrado al campo, y botón principal a ancho completo.
- En pantallas ≤ 900 px el panel de identidad se reduce a una franja compacta,
  conservando el `h1`, y el formulario ocupa el ancho útil.

El marcado mantiene sin cambios `#form-login`, `method="post"`, `data-return-url`,
`data-motivo`, `#usuario` (`name="login"`), `#password` (`name="password"`),
los atributos `autocomplete`, `#btn-mostrar-password` con `aria-pressed` y
`aria-controls`, `#btn-iniciar-sesion`, `#login-mensaje` (`role="alert"`) y
`#login-info` (`role="status"`). No se tocó `login.js`.

## Inicio
Reemplaza el contenido provisional por:
- Encabezado con etiqueta de contexto, `h1` "VisorDatosSIG 2026" y la descripción
  "Plataforma para migración, consulta y visualización de información geográfica sobre SQL Server".
- Acción según el estado de sesión: "Iniciar sesión" (anónimo) o
  "Abrir visor cartográfico" (autenticado), conservando `data-sesion-anonimo`,
  `data-sesion-bloque` y `data-sesion-nombre`.
- Sección "Capas contempladas por el proyecto" con tarjetas informativas de
  Manzanas, Lotes, Códigos Fijos y Vías, cada una con la etiqueta "Prevista" y una
  nota explícita de que **aún no están disponibles en la aplicación web**.
  No se declara ninguna funcionalidad que no exista.

## Visor
Sin ningún cambio en la lógica de Leaflet ni en `js/visor/*.js`:
- Panel lateral de 320 px con superficie blanca, borde, radio y sombra, encabezado
  "Capas", la lista de capas previstas y una nota aclaratoria.
- Mapa a ancho completo con radio, borde sutil y `overflow: hidden`; altura
  `calc(100vh - cabecera - espacios)` en escritorio con mínimo de 480 px, y
  420–440 px en tablet/móvil.
- El contenedor `#map` **no** recibe padding, para no romper el render de Leaflet.
- Se mantiene `#visor-aviso` (`role="status"`) como contenedor de texto, porque el
  guard escribe en él con `textContent`.

## Responsive verificado
Medido en navegador sobre las tres rutas, con el viewport CSS real:

| Ancho | Inicio | Login | Visor | Desbordamiento horizontal |
|---|---|---|---|---|
| **1366 px** | 4 tarjetas de capas (299 px) en 1 fila; contenedor 1280 px; header 64 px | Tarjeta 880 px con panel lateral + formulario | Panel 320 px + mapa 896 × 840 px | **No** |
| **768 px** | 3 tarjetas de capas (237 px) | Tarjeta 520 px, panel de identidad en franja compacta | Una columna: panel y mapa a 736 px; mapa 440 px de alto | **No** |
| **360 px** | 1 tarjeta por fila (308 px) | Tarjeta 328 px, franja compacta con el `h1` conservado | Una columna: panel y mapa a 308 px; mapa 420 px de alto | **No** |

En la cabecera, por debajo de 768 px la navegación pasa a una segunda fila y el
bloque de sesión se mantiene a la derecha con truncado por elipsis del nombre.

## Evidencia visual versionada

Capturas reales incorporadas al repositorio en
`docs/11_Evidencias/FrontendSIG/`, con su registro completo en
[`docs/11_Evidencias/FE-UI-1_Registro_Evidencias.md`](../11_Evidencias/FE-UI-1_Registro_Evidencias.md):

| Archivo | Vista | Resolución real |
|---|---|---|
| `04_home_ui_1366.png` | Inicio — escritorio (anónimo) | 1366 × 1000 px |
| `05_login_ui_1366.png` | Login — escritorio (anónimo) | 1366 × 1000 px |
| `06_visor_ui_1366.png` | Visor — escritorio (autenticado, Leaflet) | 1025 × 900 px |
| `07_home_ui_360.png` | Inicio — móvil (autenticado) | 270 × 820 px |
| `08_login_ui_360.png` | Login — móvil (autenticado) | 270 × 820 px |
| `09_visor_ui_360.png` | Visor — móvil (autenticado, Leaflet) | 270 × 820 px |

Las capturas de 360 px miden 270 px de ancho porque el navegador tenía zoom al
75 % (360 × 0,75 = 270): el layout capturado corresponde al viewport CSS de
360 px, no a 270. Las capturas etiquetadas como 1366/768/360 documentan el ancho
**CSS** verificado, no el tamaño en píxeles del archivo.

No se versionó ninguna captura defectuosa ni redundante. El detalle de las
imágenes descartadas (recortes a 360 px y vistas de 768 px) está en el registro
de evidencias. La verificación a **768 px** es por medición y no tiene captura
asociada en esta fase.

## Accesibilidad
- Enlace "Saltar al contenido" y `<main id="contenido">`.
- `:focus-visible` global con contorno de acento de 3 px, aplicado también a
  campos y botones (el campo enfocado añade además un anillo suave).
- Contraste: texto principal `#17293d` sobre superficies claras y estados con
  color propio y fondo tintado; los mensajes de error, aviso e información usan
  combinaciones con contraste suficiente.
- `aria-label` en la navegación principal y en el enlace de marca; todos los SVG
  decorativos llevan `aria-hidden="true"` y `focusable="false"`.
- Se conservan `role="alert"`, `role="status"`, `aria-pressed`, `aria-controls`,
  `aria-busy` y `<label for>` de todos los campos.
- Navegación y acciones con elementos reales (`<a>` para navegar, `<button>` para actuar).
- `prefers-reduced-motion` reduce las transiciones y detiene el indicador de carga.

## Pruebas ejecutadas
1. `dotnet build VisorDatosSIG.sln` → **Compilación correcta: 0 advertencias, 0 errores**.
   (Se detuvo temporalmente la instancia de la Web que bloqueaba `apphost.exe` y se
   volvió a levantar.)
2. Se levantó la Web y se comprobó por HTTP: `/` → 200, `/Cuenta/Login` → 200,
   `/Visor` → 200, `/css/visor.css` → 200, `/css/auth.css` → 200.
3. Se verificó que el HTML servido conserva todos los identificadores y `data-*`
   que consumen `guard.js` y `login.js`.
4. **Login real contra la API activa (`http://localhost:5080`)**: credenciales
   incorrectas → la petición `POST /api/autenticacion/iniciar` se ejecuta
   (CORS correcto) y la interfaz muestra "Usuario o contraseña incorrectos." con el
   estilo de alerta de error (icono en `#a4231c`). Credenciales válidas del entorno
   de desarrollo → **200, sesión guardada y redirección a `/Visor`**.
5. **Guard**: con sesión válida, `/Visor` ejecuta `GET /api/autenticacion/me`,
   no queda en estado `sesion-verificando` y el mapa queda operativo
   (`opacity: 1`, `pointer-events: auto`) con el aviso oculto.
6. **Logout**: "Cerrar sesión" redirige a
   `/Cuenta/Login?motivo=sesion-finalizada`, limpia `sessionStorage`, muestra
   "Ha cerrado sesión correctamente." y la cabecera vuelve al estado anónimo.
7. **Leaflet**: el mapa renderiza OpenStreetMap (se observó la zona de San Ignacio
   de Velasco) con atribución, controles de zoom y ~20–25 tiles cargados. La
   inicialización no se alteró.
8. **Consola del navegador**: sin errores ni advertencias durante el flujo completo.
9. **Responsive**: medido a 1366, 768 y 360 px en las tres rutas, sin
   desbordamiento horizontal en ningún caso.
10. **Sesión en `sessionStorage`**: sigue conteniendo exactamente `accessToken`,
    `usuario` y `expiresAt`.

## Resultado
- Identidad visual aplicada y coherente en cabecera, Inicio, Login y Visor.
- Sistema de tokens centralizado; `auth.css` reutiliza los tokens y componentes de
  `visor.css` sin duplicar colores, medidas ni botones.
- Autenticación intacta: login, guard, `/me`, logout, `sessionStorage`, `returnUrl`
  y el manejo de 400/401/403/timeout se comportan igual que en FE-AUTH 1.
- Leaflet intacto: el mapa base continúa renderizando correctamente.
- Build limpio (0 errores, 0 advertencias) y consola sin errores.

## Archivos modificados
- `src/VisorDatosSIG.Web/Views/Shared/_Layout.cshtml`
- `src/VisorDatosSIG.Web/Views/Home/Index.cshtml`
- `src/VisorDatosSIG.Web/Views/Cuenta/Login.cshtml`
- `src/VisorDatosSIG.Web/Views/Visor/Index.cshtml`
- `src/VisorDatosSIG.Web/wwwroot/css/visor.css`
- `src/VisorDatosSIG.Web/wwwroot/css/auth.css`
- `docs/09_VisorSIG/FE-UI-1_Identidad_Visual.md` (este documento)
- `docs/09_VisorSIG/README.md` (índice de incrementos)

**No se modificó**: `VisorDatosSIG.Api`, `VisorDatosSIG.Application`,
`VisorDatosSIG.Infrastructure`, `VisorDatosSIG.Domain`, `VisorDatosSIG.Migrador`,
`database/`, los módulos `js/visor/*.js` ni los módulos `js/auth/*.js`.
Tampoco se alteraron `sessionStorage`, el JWT, los endpoints, `Api:BaseUrl`, el
guard, el logout, `returnUrl` ni CORS.

## Limitaciones
- La identidad se resuelve solo con CSS propio y SVG inline; no se incorporaron
  iconos de una librería. Los mensajes usan iconografía por máscara CSS.
- El panel de identidad del login se reduce (no se oculta) por debajo de 900 px
  para conservar el `h1` de la página.
- Los recursos estáticos se sirven con ETag; durante la verificación fue necesario
  forzar una recarga sin caché para que el navegador tomara el CSS actualizado.
- No se agregó un pie de página ni contenido nuevo al Inicio más allá de las
  capas previstas; queda como mejora opcional futura.

## Siguiente fase
Continuar con FE-SIG 2 (CodigosFijos) reutilizando el sistema de tokens y el panel
de capas ya preparado.
