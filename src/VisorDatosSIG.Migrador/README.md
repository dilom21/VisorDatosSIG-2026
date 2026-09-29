# VisorDatosSIG.Migrador

Aplicación de escritorio responsable de seleccionar, validar, previsualizar y migrar los archivos SHP hacia SQL Server.

Flujo previsto:
1. Selección de SHP.
2. Validación SHP/SHX/DBF/PRJ.
3. Verificación WGS 84 / SRID 4326.
4. Previsualización y mapeo.
5. Carga transaccional por lotes.
6. Progreso, cancelación, bitácora y resumen.

## Estado actual

| Fase | Alcance | Estado |
|---|---|---|
| 1 | Selección del SHP, detección de capa, validación de archivos asociados, verificación WGS 84 / SRID 4326, lectura de atributos y previsualización | Implementada |
| 2 | Validación detallada registro por registro, estadísticas, incidencias, progreso y cancelación | Implementada |
| 2.5A | Backend de autenticación en la API (JWT, roles y bitácora) | Implementada |
| 2.5B | Autenticación visual en el Migrador: inicio de sesión, sesión en memoria, cabecera con el usuario, «Mi cuenta» y cierre de sesión | Implementada |
| 3 | Migración SHP → SQL Server (requerirá sesión autenticada) | Pendiente |

Las fases 1 y 2 funcionan **sin** sesión: el usuario puede seleccionar, inspeccionar, previsualizar y
validar archivos sin autenticarse. La migración (fase 3) será la primera operación que exija sesión.

## Arquitectura de autenticación

```text
Migrador WinForms  --HTTP/JSON-->  VisorDatosSIG.Api  -->  SQL Server
 (token en memoria)                 (JWT + Bitacora)      (dbo.Usuario)
```

El Migrador **nunca** se conecta a SQL Server ni conoce la cadena de conexión, la clave JWT,
los hashes, los salts o cualquier credencial interna. Solo consume los endpoints:

| Método | Ruta | Uso en el Migrador |
|---|---|---|
| POST | `/api/autenticacion/iniciar` | `LoginForm` (tarjeta de inicio de sesión) |
| GET | `/api/autenticacion/me` | `AccountForm` («Mi cuenta») valida la sesión y refresca los datos |
| POST | `/api/autenticacion/cerrar` | Opción «Cerrar sesión» del menú del usuario |

El token JWT vive **únicamente en memoria** (`UserSession`): no se guarda en archivos, JSON,
configuración, registro de Windows ni bitácora, y desaparece al cerrar la aplicación.

## Configuración de la API

La dirección de la API se define en un único lugar: `Configuration/ApiSettings.cs`
(`http://localhost:5080` por omisión) y puede cambiarse **sin recompilar** con la variable de entorno:

```bat
set VISORDATOSSIG_API_URL=http://localhost:5080
set VISORDATOSSIG_API_URL=https://api.visordatos.ejemplo/   :: producción
```

La dirección no es un secreto; se usa para construir las peticiones y para mostrarla en la interfaz
(tooltips). El `HttpClient` se crea una sola vez por aplicación (`Program.cs`), con el tiempo de espera
centralizado (15 segundos) y se reutiliza en todas las peticiones.

## Cómo ejecutar (desarrollo)

```bat
:: 1) API: el perfil de desarrollo (Properties\launchSettings.json) ya escucha en
::    http://localhost:5080 y activa el entorno Development (Swagger disponible).
::    No requiere --urls ni variables de entorno.
dotnet run --project src\VisorDatosSIG.Api

:: 2) Migrador (usa la misma dirección; puede cambiarse con VISORDATOSSIG_API_URL)
dotnet run --project src\VisorDatosSIG.Migrador
```

La API necesita `ConnectionStrings:VisorDatosSIG` y `Jwt:Key` (ver `src/VisorDatosSIG.Api/README.md`).
Usuario de desarrollo: `admin` / `Admin123!` (rol Administrador).

## Estructura de la fase 2.5B

```text
VisorDatosSIG.Migrador/
├── Configuration/
│   └── ApiSettings.cs              Dirección de la API, tiempo de espera y HttpClient reutilizable
├── Services/
│   ├── AuthenticationApiClient.cs  Iniciar sesión, consultar /me y cerrar sesión (Bearer)
│   └── ResultadoApi.cs             Estados de operación y mensajes para el usuario
├── Session/
│   └── UserSession.cs              Sesión en memoria: token, usuario, roles y vencimiento
├── Controls/
│   ├── EstiloUI.cs                 Paleta, tipografías y utilidades de dibujo
│   ├── GlifoDibujo.cs              Iconos de línea dibujados con GDI+
│   ├── GradientButton.cs           Botón principal (degradado, hover, pressed, carga, foco)
│   ├── CardPanel.cs                Tarjeta blanca con esquinas redondeadas y sombra
│   ├── ModernTextBox.cs            Campo redondeado con icono y botón mostrar/ocultar contraseña
│   ├── BarraAcento.cs              Barra decorativa con degradado
│   ├── AvatarInicial.cs            Avatar circular con las iniciales
│   ├── UserChipButton.cs           Identificación del usuario en la cabecera
│   ├── MenuSesionRenderer.cs       Menú del usuario con la paleta del proyecto
│   └── MenuUsuario.cs              Encabezado + «Mi cuenta» / «Cerrar sesión»
└── Forms/
    ├── LoginForm.cs / .Layout.cs / .Presentation.cs    Inicio de sesión
    ├── AccountForm.cs / .Layout.cs / .Presentation.cs  «Mi cuenta» (solo lectura)
    ├── MigradorForm.Session.cs                        Cabecera, menú, Mi cuenta y cierre de sesión
    ├── MigradorForm.cs                                Ventana principal (fases 1 y 2)
    ├── MigradorForm.Layout.cs                         Construcción de la interfaz
    └── MigradorForm.Presentation.cs                   Presentación de resultados
```

### Diseño de la ventana de inicio de sesión

- Tarjeta blanca centrada, esquinas redondeadas, borde violeta suave y sombra discreta.
- Degradado de identidad: violeta `#B517F0` → azul `#2F80ED`; acento violeta `#7C3AED`.
- Campos modernos con icono de línea, foco violeta y texto de ayuda discreto.
- Contraseña oculta por defecto (`UseSystemPasswordChar`) con botón para mostrarla u ocultarla.
- Botón principal con degradado, flecha, indicador de carga y bloqueo del doble clic.
- Mensajes de estado dentro de la tarjeta (no depende de cuadros de diálogo).
- Enter ejecuta el inicio de sesión y Escape cancela o cierra la ventana.
- No se incluyen «Recordarme», «Olvidé mi contraseña» ni «Registrarse»: están fuera del alcance.

### Mensajes de error al usuario

| Situación | Mensaje |
|---|---|
| HTTP 401 en el inicio de sesión | Usuario o contraseña incorrectos. |
| HTTP 403 en el inicio de sesión | No fue posible iniciar sesión con esta cuenta. |
| API no disponible | No fue posible comunicarse con el servidor de autenticación. |
| Tiempo de espera agotado | El servidor tardó demasiado en responder. |
| Datos incompletos (HTTP 400) | Revise el usuario y la contraseña ingresados. |
| Petición protegida con 401 | Su sesión ha expirado. Inicie sesión nuevamente. |

Nunca se muestran trazas, `SqlException`, `HttpRequestException`, JSON técnico ni detalles internos.
Si una petición protegida devuelve 401, la sesión se limpia y la cabecera vuelve al estado no autenticado.

## Pruebas

```bat
dotnet test VisorDatosSIG.sln
```

- `tests\VisorDatosSIG.Migrador.UnitTests`: configuración de la API, sesión en memoria y consumo HTTP
  (con un manejador de mensajes falso, sin API ni SQL Server).
- `tests\VisorDatosSIG.UnitTests`: autenticación de la API, PBKDF2, JWT, controlador y validaciones.

Prueba manual (requiere la API en ejecución): sin sesión, inicio de sesión correcto, «Mi cuenta»,
cierre de sesión, contraseña incorrecta y API caída. Después puede comprobarse la bitácora:

```sql
SELECT TOP 20 * FROM dbo.Bitacora ORDER BY FechaHora DESC;
```


