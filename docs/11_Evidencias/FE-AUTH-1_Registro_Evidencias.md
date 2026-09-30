# Registro de evidencias — FE-AUTH 1 (Login Web, sesión y guard)

- **Fase:** FE-AUTH 1
- **Rama:** `dev-josias`
- **Caso de prueba asociado:** `P-FE-02` (**APROBADO** en el flujo principal)
- **Entorno:**
  - Web: `http://localhost:5000` (`dotnet run --project src/VisorDatosSIG.Web/VisorDatosSIG.Web.csproj`)
  - API: `http://localhost:5080`, ejecutada desde su propio worktree
  - SQL Server en `localhost`, base de datos `VisorDatosSIG`
  - Usuario de desarrollo: `admin` (contraseña no documentada)

## Evidencia observada (ejecución manual real)

Ejecutada por el responsable del módulo Web en navegador, contra la API real.
Observaciones registradas:

- `/Cuenta/Login` carga correctamente.
- Login real contra el backend: `POST http://localhost:5080/api/autenticacion/iniciar` → **200 OK**.
- CORS entre `http://localhost:5000` y `http://localhost:5080` funcionando.
- La sesión se almacena en `sessionStorage` con `accessToken`, `usuario` y `expiresAt`.
- Redirección correcta a `/Visor` después del login.
- El guard ejecuta `GET /api/autenticacion/me` → **200 OK**.
- Usuario autenticado reconocido: login `admin`, nombre `Administrador`, rol `Administrador`.
- El visor queda habilitado después de validar `/me`.
- Logout correcto: `POST /api/autenticacion/cerrar`; `sessionStorage` limpio; la
  interfaz vuelve al estado anónimo y redirige al Login.
- Acceder directamente a `/Visor` sin sesión redirige al Login.
- Contraseña incorrecta → **401** y el frontend muestra "Usuario o contraseña incorrectos.".
- Campos vacíos no generan peticiones HTTP.
- Sin errores JavaScript relevantes en consola durante el flujo correcto.
- El frontend fue comprobado visualmente y es utilizable.

## Evidencia en base de datos

| Verificación | Resultado |
|---|---|
| Tablas `Usuarios`, `Roles`, `UsuariosRoles`, `Bitacora` | Presentes |
| Usuario `admin` | Presente |
| Roles `Administrador` y `Consultor` | Presentes |
| Relación `admin` → `Administrador` | Presente |
| Registros de bitácora por Login y Logout | Presentes |

> No se documentan el token JWT real, la clave de firma, los hashes o salts de
> contraseña, las cadenas de conexión ni la contraseña utilizada en la prueba.

## Evidencia en servidor (complementaria)

| Recurso | Resultado |
|---|---|
| `/` | 200 |
| `/Visor` | 200 |
| `/Cuenta/Login` | 200 |
| `/css/auth.css` | 200 |
| `/js/auth/sesion.js` | 200 |
| `/js/auth/api.js` | 200 |
| `/js/auth/login.js` | 200 |
| `/js/auth/guard.js` | 200 |
| `/css/visor.css` | 200 |
| `/js/visor/map.js` | 200 |
| `/lib/leaflet/leaflet.js` | 200 |
| `dotnet build VisorDatosSIG.sln` | Compilación correcta: 0 advertencias, 0 errores |

## Archivos de evidencia

| Archivo esperado | Estado |
|---|---|
| `docs/11_Evidencias/02_login_formulario.png` | **PENDIENTE DE ADJUNTAR** |
| `docs/11_Evidencias/03_sesion_activa_header.png` | **PENDIENTE DE ADJUNTAR** |
| `docs/11_Evidencias/04_visor_habilitado.png` | **PENDIENTE DE ADJUNTAR** |
| `docs/11_Evidencias/05_consulta_bitacora.png` | **PENDIENTE DE ADJUNTAR** |

> Nota: no se declara la existencia de capturas que no estén realmente en el
> repositorio. Cuando se adjunten, actualizar esta tabla.

## Casos pendientes de ejecución

- 403 con usuario inactivo/deshabilitado.
- Expiración natural del JWT esperando los 30 minutos reales.
- 400 real por datos inválidos.
- 401 real por token ausente, inválido o vencido.
- Prueba visual responsive a 1366 / 768 / 360 px.

## Incidencias

Ninguna durante el flujo end-to-end.
