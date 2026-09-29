# P-FE-02 — Login Web, sesión en el navegador y guard del visor

- **Fase:** FE-AUTH 1
- **Módulo:** `src/VisorDatosSIG.Web`
- **Tipo:** prueba manual de funcionamiento end-to-end
- **Estado:** **APROBADO** (flujo principal) — verificado manualmente por el
  responsable por HTTP y en navegador sobre `http://localhost:5000` contra la API
  real en `http://localhost:5080`.

> Quedan subcasos **PENDIENTE DE EJECUCIÓN** que no fueron probados realmente
> (usuario inactivo → 403, expiración natural del JWT a 30 minutos, 400 real y 401
> real por token inválido o vencido). No se declara ningún resultado que no haya
> sido observado. La aprobación general aplica al flujo principal.

## Precondiciones
- Rama `dev-josias`.
- `dotnet build VisorDatosSIG.sln` correcto (0 errores, 0 advertencias).
- **Web:** `dotnet run --project src/VisorDatosSIG.Web/VisorDatosSIG.Web.csproj --urls http://localhost:5000`
- **API:** `http://localhost:5080`, ejecutada desde su propio worktree del compañero
  (no se modificó ni se ejecutó desde `dev-josias`).
- **SQL Server** en `localhost`, base de datos `VisorDatosSIG`, con el esquema de
  seguridad (`Usuarios`, `Roles`, `UsuariosRoles`, `Bitacora`) ya sembrado.
- CORS de la API habilitado para el origen `http://localhost:5000`.
- Usuario de desarrollo: `admin`. La contraseña se usó únicamente durante la prueba
  y **no se documenta** como información sensible persistente.
- Conexión a Internet (mapa base de OpenStreetMap en `/Visor`).

## Pasos
1. Abrir `/Cuenta/Login` y comprobar el formulario.
2. Intentar enviar con los campos vacíos.
3. Introducir una contraseña incorrecta y enviar.
4. Introducir las credenciales correctas y enviar.
5. Comprobar el almacenamiento de la sesión y la redirección.
6. Comprobar la llamada del guard a `/me` y la habilitación del visor.
7. Pulsar "Cerrar sesión" y comprobar el estado final.
8. Abrir `/Visor` directamente sin sesión.
9. Revisar la consola del navegador durante el flujo correcto.
10. Comprobar los registros de bitácora de inicio y cierre de sesión.

## Resultado esperado
- El formulario renderiza y es utilizable con teclado y ratón.
- Los campos vacíos no generan ninguna petición HTTP.
- Las credenciales incorrectas no permiten el acceso.
- Las credenciales correctas abren la sesión y habilitan el visor solo tras validar `/me`.
- El logout limpia la sesión y devuelve la interfaz al estado anónimo.
- Sin errores JavaScript relevantes en consola.
- El inicio y el cierre de sesión quedan registrados en la bitácora.

## Resultado obtenido

### A. Prueba end-to-end real — **APROBADO**

| Subcaso | Resultado |
|---|---|
| `/Cuenta/Login` carga correctamente | **APROBADO** |
| Login correcto contra la API | **APROBADO** |
| `POST /api/autenticacion/iniciar` → 200 OK | **APROBADO** |
| CORS Web (`:5000`) → API (`:5080`) | **APROBADO** |
| `sessionStorage` guarda la sesión (`accessToken`, `usuario`, `expiresAt`) | **APROBADO** |
| Redirección a `/Visor` tras el login | **APROBADO** |
| Guard ejecuta `GET /api/autenticacion/me` → 200 OK | **APROBADO** |
| Usuario reconocido: `admin` / `Administrador` / rol `Administrador` | **APROBADO** |
| Visor habilitado tras validar `/me` | **APROBADO** |
| Logout (`POST /api/autenticacion/cerrar`) | **APROBADO** |
| Limpieza de `sessionStorage` tras el logout | **APROBADO** |
| Interfaz vuelve al estado anónimo y redirige al Login | **APROBADO** |
| `/Visor` sin sesión redirige al Login | **APROBADO** |
| Contraseña incorrecta → 401 con "Usuario o contraseña incorrectos." | **APROBADO** |
| Campos vacíos → 0 peticiones HTTP | **APROBADO** |
| Bitácora de Login y Logout | **APROBADO** |
| Consola JavaScript sin errores relevantes en el flujo correcto | **APROBADO** |

### B. Esquema de seguridad en base de datos — **APROBADO**

| Verificación | Resultado |
|---|---|
| Existen las tablas `Usuarios`, `Roles`, `UsuariosRoles`, `Bitacora` | **APROBADO** |
| Existe el usuario `admin` | **APROBADO** |
| Existen los roles `Administrador` y `Consultor` | **APROBADO** |
| Existe la relación `admin` → `Administrador` | **APROBADO** |
| Login y Logout generan registros en `Bitacora` | **APROBADO** |
| Frontend comprobado visualmente y utilizable | **APROBADO** |

### C. Verificación estructural complementaria — **APROBADO**

Ejecutada previamente desde el entorno de desarrollo, sin la API:

| Verificación | Resultado |
|---|---|
| `dotnet build VisorDatosSIG.sln` | **APROBADO** — 0 advertencias, 0 errores |
| `/`, `/Visor`, `/Cuenta/Login` → HTTP 200 | **APROBADO** |
| `/css/auth.css`, `/css/visor.css` → HTTP 200 | **APROBADO** |
| `js/auth/sesion.js`, `api.js`, `login.js`, `guard.js` → HTTP 200 | **APROBADO** |
| `js/visor/map.js`, `lib/leaflet/leaflet.js` → HTTP 200 | **APROBADO** |
| Inyección de `Api.BaseUrl` (`apiBaseUrl: "http://localhost:5080"`) | **APROBADO** |
| Foco inicial en el campo Usuario | **APROBADO** |
| Mostrar/ocultar contraseña (`type` y `aria-pressed`) | **APROBADO** |
| Estado de carga (`disabled`, "Iniciando sesión...", `aria-busy`) | **APROBADO** |
| Evitar doble submit (3 clics rápidos → 1 petición) | **APROBADO** |
| Forma de la petición: `POST /iniciar`, `application/json`, claves `login` y `password`, sin `Authorization` | **APROBADO** |
| Error de red (API no disponible) → "No fue posible comunicarse con el servidor." | **APROBADO** |
| Fail-closed del guard ante `/me` no validable (aviso, "Reintentar", mapa no utilizable) | **APROBADO** |
| Auto-expiración local por `expiresAt` | **APROBADO** |
| `returnUrl` no local rechazado (`https://…`, `//…` → `/Visor`) | **APROBADO** |
| Cabecera y Home reflejan el estado de sesión | **APROBADO** |
| Solo se persisten 3 claves en `sessionStorage` | **APROBADO** |
| Ausencia de token o contraseña en consola | **APROBADO** |

### D. Pendientes de ejecución — **PENDIENTE DE EJECUCIÓN**

No fueron probados realmente. No se reporta ningún resultado para ellos.

| Subcaso | Estado |
|---|---|
| 403 real con usuario inactivo/deshabilitado | **PENDIENTE DE EJECUCIÓN** |
| Expiración natural del JWT esperando los 30 minutos reales | **PENDIENTE DE EJECUCIÓN** |
| 400 real por datos inválidos | **PENDIENTE DE EJECUCIÓN** |
| 401 real por token ausente, inválido o vencido en `/me` y `/cerrar` | **PENDIENTE DE EJECUCIÓN** |
| Prueba visual responsive del formulario a 1366 / 768 / 360 px | **PENDIENTE DE EJECUCIÓN** |

## Evidencia
Registrada en `docs/11_Evidencias/FE-AUTH-1_Registro_Evidencias.md` y en
`docs/09_VisorSIG/FE-AUTH-1_Login_Web.md`.

- Comprobación manual end-to-end por el responsable sobre `http://localhost:5000`
  contra la API real en `http://localhost:5080`.
- Comprobación del almacenamiento de la sesión, de la redirección, del guard y del
  logout desde el navegador.
- Comprobación del esquema de seguridad y de los registros de bitácora en la base
  de datos `VisorDatosSIG`.
- **Pendiente adjuntar capturas de pantalla** del formulario, del estado de sesión
  y de las consultas de bitácora en `docs/11_Evidencias/`.

## Incidencias
Ninguna durante el flujo end-to-end.

Durante la instrumentación previa de las pruebas se observó una vez un conteo de
dos peticiones en un escenario con estado de página previamente manipulado; al
repetir la prueba sobre una carga limpia y con clics rápidos el resultado fue
**1 submit / 1 petición**, confirmando que el bloqueo de doble submit funciona y
que el conteo previo fue un artefacto de la instrumentación, no del código.

## Limitaciones
- Los subcasos de la sección D quedan pendientes de ejecución.
- La protección de página es del lado del cliente: el HTML de `/Visor` es
  descargable sin sesión; la protección real de los datos está en la API.
- La prueba se realizó sobre HTTP en `localhost`; fuera de `localhost` la
  contraseña viajaría sin cifrado.
- `sessionStorage` es legible por JavaScript: un XSS podría robar el token
  (mitigado con `textContent`, sin `innerHTML` y sin scripts de terceros).
- Queda pendiente adjuntar las capturas de evidencia visual.
