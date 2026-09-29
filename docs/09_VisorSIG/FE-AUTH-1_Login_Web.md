# FE-AUTH 1 — Login Web, sesión en el navegador y guard del visor

- **Fase:** FE-AUTH 1
- **Módulo:** `src/VisorDatosSIG.Web`
- **Rama:** `dev-josias`
- **Estado:** **APROBADO** (flujo principal) — validado end-to-end contra la API real.
  Algunos subcasos quedan **PENDIENTE DE EJECUCIÓN** (ver la sección final).

## Objetivo
Implementar el inicio de sesión de `VisorDatosSIG.Web` consumiendo **directamente
desde el navegador** el contrato de autenticación real de la API, manteniendo la
sesión en `sessionStorage` y protegiendo el acceso a `/Visor` mediante una
validación real contra `GET /api/autenticacion/me`.

## Entorno validado

| Componente | Valor |
|---|---|
| Web (frontend de este módulo) | `http://localhost:5000` |
| API (backend real) | `http://localhost:5080` |
| Motor de base de datos | SQL Server en `localhost` |
| Base de datos | `VisorDatosSIG` |
| Usuario de desarrollo | `admin` |
| Rol del usuario | `Administrador` |
| Nombre mostrado | `Administrador` |

La API se ejecuta desde su propio worktree del compañero. Desde `dev-josias` no se
modificó ni se ejecutó ningún componente del backend, ni el esquema SQL.

La contraseña se utilizó únicamente durante la prueba de integración y **no se
documenta** en este repositorio. Tampoco se documentan el token JWT real, la clave
de firma, los hashes o salts de contraseña, ni cadenas de conexión con credenciales.

## Alcance de esta fase
- Formulario de login en `/Cuenta/Login`.
- Cliente HTTP JavaScript centralizado (`VisorSIG.api`).
- Administración de la sesión en el navegador (`VisorSIG.sesion`) con `sessionStorage`.
- Guard de página (`VisorSIG.guard`) para `/Visor`.
- Estado de sesión en la cabecera (nombre, rol y cerrar sesión).
- Auto-expiración local basada en `expiresAt`.
- Inyección de la URL de la API desde Razor (`@Json.Serialize`), sin hardcodearla en varios JS.

## Fuera de alcance
- Cambios en `VisorDatosSIG.Api`, `VisorDatosSIG.Application`,
  `VisorDatosSIG.Infrastructure`, `VisorDatosSIG.Domain`, `VisorDatosSIG.Migrador`
  ni en `database/`.
- Refresh token, "Recordarme", recuperación de contraseña, registro o MFA.
- Cookies, `localStorage`, almacenamiento del token en el servidor Web, archivos o logs.
- Capas, GeoJSON e identificación del visor (fases FE-SIG posteriores).

## Arquitectura final
```
Navegador                                   API  http://localhost:5080
http://localhost:5000
├── VisorSIG.sesion  →  sessionStorage
├── VisorSIG.api     ──fetch + Authorization: Bearer──▶  /api/autenticacion/*
└── VisorSIG.guard   →  protección de página y estado de la UI
                                │
                                └── SQL Server localhost / VisorDatosSIG
                                    (Usuarios, Roles, UsuariosRoles, Bitacora)
```

- `wwwroot/js/auth/sesion.js` → `VisorSIG.sesion`: `guardar`, `obtenerToken`,
  `obtenerUsuario`, `obtenerExpiracion`, `estaAutenticado`, `estaExpirada`,
  `actualizarUsuario`, `limpiar`, `nombreParaMostrar`, `rolesTexto`.
  Todos los accesos a `sessionStorage` van en `try/catch`: si no está disponible,
  la sesión se comporta como "no autenticado" y nunca rompe la página.
- `wwwroot/js/auth/api.js` → `VisorSIG.api`: base URL centralizada, `fetch`,
  `Content-Type: application/json`, `Authorization: Bearer`, `AbortController`
  con timeout de 10 s, parseo seguro de JSON, manejo de 204 y de errores de red.
  `peticion()` **nunca lanza**: siempre resuelve `{ ok, status, datos, error }`.
- `wwwroot/js/auth/guard.js` → `VisorSIG.guard`: `protegerPagina`, `actualizarHeader`,
  `iniciarAutoExpiracion`, `cerrarSesion`, `redirigirALogin`.
- `wwwroot/js/auth/login.js`: comportamiento del formulario de login.
- `_Layout.cshtml` inyecta la configuración y carga `sesion.js`, `api.js` y
  `guard.js` en todas las páginas.

La URL de la API se inyecta una sola vez desde Razor:
```cshtml
window.VisorSIG.config = { apiBaseUrl: @Json.Serialize(apiBaseUrl) };
```
con `apiBaseUrl` leído de `Configuration["Api:BaseUrl"]`. No es un secreto y no
está hardcodeada en ningún módulo JavaScript.

## Contrato HTTP consumido
### `POST /api/autenticacion/iniciar`
Request (`application/json`) con las claves `login` y `password`.
Response 200 con `accessToken`, `expiresIn` (segundos) y `usuario`
(`idUsuario`, `login`, `nombre`, `roles`).

### `GET /api/autenticacion/me`
Requiere `Authorization: Bearer <accessToken>`. Devuelve 200 con el objeto
`usuario` o 401 si el token falta, es inválido o venció.

### `POST /api/autenticacion/cerrar`
Requiere `Authorization: Bearer <accessToken>`. Devuelve 204 (registra el cierre
en la bitácora) o 401. No invalida el token: el backend no mantiene lista de
revocación, por lo que el cierre efectivo es responsabilidad del cliente.

### Códigos de error de `/iniciar`
| Código | Significado |
|---|---|
| 400 | Datos incompletos o inválidos |
| 401 | Login o contraseña incorrectos |
| 403 | El usuario existe pero está inactivo/deshabilitado |

Regla de implementación: **el cliente decide siempre por el código de estado HTTP**.
Un 401 puede llegar **sin cuerpo JSON**, porque lo emite el middleware de
autenticación del backend y no el controlador; nunca se asume que el cuerpo
existe o es JSON.

## Estrategia de sesión
Se usa **`sessionStorage`**, con una única clave `visorSIG.sesion` que contiene
exactamente tres datos:
- `accessToken`
- `usuario` (datos públicos)
- `expiresAt`

`expiresAt` se calcula como `Date.now() + expiresIn * 1000`. Si `expiresIn` no es
un número mayor que cero, la sesión se guarda con `expiresAt = 0` y se trata como
expirada de inmediato (no se inventa una duración por defecto).

**No se usa**: `localStorage`, cookies, variables globales como única
persistencia, almacenamiento del JWT en el servidor Web, archivos ni logs.

La sesión se pierde al cerrar la pestaña y no se comparte entre pestañas: es la
ventana de exposición más acotada compatible con una aplicación MVC de navegación
completa, sin estado en el servidor y sin requerir HTTPS.

## Flujo Login → JWT → /Visor → /me → Logout

### 1. Login (`/Cuenta/Login`)
1. Validación local: campos vacíos **no** generan ninguna petición HTTP.
2. Durante la petición: botón deshabilitado, `aria-busy="true"`, texto
   "Iniciando sesión..." y bloqueo de doble submit.
3. `POST /api/autenticacion/iniciar` (CORS habilitado entre `:5000` y `:5080`).
4. 200 → guardar la sesión en `sessionStorage`, limpiar el campo de contraseña y
   redirigir a `/Visor` (o al `returnUrl` validado).

### 2. Guard (`/Visor`)
1. ¿Hay sesión? No → `/Cuenta/Login?returnUrl=/Visor`.
2. ¿Expiró localmente? Sí → limpiar y `/Cuenta/Login?motivo=expirada&returnUrl=/Visor`.
3. Vigente → `GET /api/autenticacion/me` con Bearer:
   - 200 → actualizar los datos públicos del usuario y **habilitar el visor**.
   - 401 → limpiar sesión y `/Cuenta/Login?motivo=expirada&returnUrl=/Visor`.
   - Error de red / timeout → **fail-closed**: no se borra la sesión, pero
     tampoco se habilita el visor; se muestra un aviso y un botón "Reintentar".

No se oculta la página en lugar de validar: la validación contra `/me` es
obligatoria y el mapa queda con `pointer-events: none` mientras no se valide.

### 3. Logout
`POST /api/autenticacion/cerrar` (best-effort) → **siempre** limpiar
`sessionStorage`, actualizar la cabecera al estado anónimo y redirigir a
`/Cuenta/Login?motivo=sesion-finalizada`. Si la API está caída, la sesión local
se cierra igualmente.

### 4. Auto-expiración
Un `setTimeout` calculado desde `expiresAt` limpia la sesión, actualiza la
cabecera y redirige con `motivo=expirada`. Es idempotente (no acumula
temporizadores). No hay refresh token.

## Manejo de errores y mensajes exactos
| Situación | Mensaje |
|---|---|
| Usuario vacío | "Ingrese su usuario." |
| Contraseña vacía | "Ingrese su contraseña." |
| HTTP 400 | "Revise los datos ingresados." |
| HTTP 401 | "Usuario o contraseña incorrectos." |
| HTTP 403 | "Su cuenta está deshabilitada. Contacte al administrador." |
| Error de red / timeout | "No fue posible comunicarse con el servidor." |
| Otro | "No fue posible iniciar sesión. Intente nuevamente." |
| Sesión expirada (motivo) | "Su sesión ha expirado. Inicie sesión nuevamente." |
| Logout (motivo) | "Ha cerrado sesión correctamente." |
| `/me` no validable | "No se pudo validar la sesión con el servidor. Verifique que la API esté disponible." |

Nunca se muestran stack traces, JSON técnico ni el token.

## Seguridad aplicada
- **`textContent`** para todo contenido dinámico; cero `innerHTML`.
- **Sin open redirect**: `returnUrl` se valida en el cliente (`login.js`) y en el
  servidor (`CuentaController.NormalizarReturnUrl`). Se aceptan únicamente rutas
  locales que empiecen con `/`, rechazando `//`, `/\`, `\` y `://`.
- **Sin logs de credenciales**: nunca se imprime la contraseña ni el token.
- **Sin almacenamiento persistente**: solo `sessionStorage`; nada en disco, cookies
  ni servidor.
- **Fail-closed** ante imposibilidad de validar `/me`.
- **Sin `[Authorize]` MVC**: la identidad vive en el navegador, por lo que proteger
  los controladores MVC daría una falsa sensación de seguridad. La protección real
  permanece en los endpoints de la API.
- El formulario usa `method="post"` para que, si JavaScript falla, la contraseña
  nunca viaje por la URL.

## Archivos creados
- `src/VisorDatosSIG.Web/Controllers/CuentaController.cs`
- `src/VisorDatosSIG.Web/Views/Cuenta/Login.cshtml`
- `src/VisorDatosSIG.Web/wwwroot/css/auth.css`
- `src/VisorDatosSIG.Web/wwwroot/js/auth/api.js`
- `src/VisorDatosSIG.Web/wwwroot/js/auth/sesion.js`
- `src/VisorDatosSIG.Web/wwwroot/js/auth/login.js`
- `src/VisorDatosSIG.Web/wwwroot/js/auth/guard.js`
- `src/VisorDatosSIG.Web/appsettings.json`
- `docs/09_VisorSIG/FE-AUTH-1_Login_Web.md` (este documento)
- `docs/10_Pruebas/P-FE-02_Login_Web.md`
- `docs/11_Evidencias/FE-AUTH-1_Registro_Evidencias.md`

## Archivos modificados
- `src/VisorDatosSIG.Web/Views/Shared/_Layout.cshtml`
  - inyección de `IConfiguration` y de `Api:BaseUrl` como `window.VisorSIG.config`;
  - carga de `auth.css` y de `sesion.js`, `api.js` y `guard.js` en todas las páginas;
  - bloque de estado de sesión en la cabecera.
- `src/VisorDatosSIG.Web/Views/Home/Index.cshtml`
  - bloques de estado de sesión (`data-sesion-anonimo` / `data-sesion-bloque`).
- `src/VisorDatosSIG.Web/Views/Visor/Index.cshtml`
  - elemento de aviso de verificación (`#visor-aviso`);
  - llamada a `VisorSIG.guard.protegerPagina({ returnUrl: '/Visor' })`.
- `docs/09_VisorSIG/README.md`, `docs/10_Pruebas/README.md` y
  `docs/11_Evidencias/README.md`: enlaces a la nueva documentación.

## Resultado end-to-end
La integración fue probada manualmente por el responsable y **funciona**:

| Verificación | Resultado |
|---|---|
| `/Cuenta/Login` carga correctamente | **APROBADO** |
| Login real contra el backend (`POST /iniciar` → 200 OK) | **APROBADO** |
| CORS Web `:5000` → API `:5080` | **APROBADO** |
| Sesión almacenada en `sessionStorage` (`accessToken`, `usuario`, `expiresAt`) | **APROBADO** |
| Redirección a `/Visor` tras el login | **APROBADO** |
| Guard ejecuta `GET /me` → 200 OK | **APROBADO** |
| Usuario reconocido: `admin` / `Administrador` / rol `Administrador` | **APROBADO** |
| Visor habilitado tras validar `/me` | **APROBADO** |
| Logout (`POST /cerrar`), limpieza de `sessionStorage` y vuelta al estado anónimo | **APROBADO** |
| `/Visor` sin sesión redirige al Login | **APROBADO** |
| Contraseña incorrecta → 401 con "Usuario o contraseña incorrectos." | **APROBADO** |
| Campos vacíos → 0 peticiones HTTP | **APROBADO** |
| Consola JavaScript sin errores relevantes | **APROBADO** |
| Frontend comprobado visualmente y utilizable | **APROBADO** |

### Base de datos y bitácora
| Verificación | Resultado |
|---|---|
| Tablas `Usuarios`, `Roles`, `UsuariosRoles`, `Bitacora` | **APROBADO** |
| Usuario `admin` | **APROBADO** |
| Roles `Administrador` y `Consultor` | **APROBADO** |
| Relación `admin` → `Administrador` | **APROBADO** |
| Login y Logout generan registros de bitácora | **APROBADO** |

El detalle por subcaso está en `docs/10_Pruebas/P-FE-02_Login_Web.md`.

## Casos pendientes de ejecución
No fueron probados realmente y no se declara ningún resultado para ellos:

- **403** con usuario inactivo/deshabilitado.
- **Expiración natural del JWT** esperando los 30 minutos reales.
- **400** real por datos inválidos.
- **401** real por token ausente, inválido o vencido en `/me` y `/cerrar`.
- Prueba visual responsive del formulario a 1366 / 768 / 360 px.

## Limitaciones
- La protección de página es del lado del cliente: la identidad vive en el
  navegador. El HTML de `/Visor` es descargable sin sesión; la información
  proviene de endpoints protegidos de la API.
- Desarrollo sobre HTTP en `localhost`: fuera de `localhost` la contraseña
  viajaría sin cifrado. HTTPS es obligatorio antes de cualquier despliegue.
- `sessionStorage` es legible por JavaScript, por lo que un XSS podría robar el
  token (mitigado con `textContent`, sin `innerHTML` y sin scripts de terceros).
- Sin lista de revocación en el backend: el logout no invalida el token.
- La sesión no se comparte entre pestañas.
- Queda pendiente adjuntar las capturas de evidencia visual.

## Siguiente fase
Continuar con FE-SIG 2 (CodigosFijos) y, cuando se requiera, ejecutar los
subcasos pendientes de la sección anterior.
