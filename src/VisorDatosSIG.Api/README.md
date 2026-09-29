# VisorDatosSIG.Api

Capa HTTP del VisorDatosSIG 2026. Expone autenticación con JWT y, más adelante,
catálogo de capas, GeoJSON, búsquedas, filtros e historial autorizado.

## Endpoints implementados

| Método | Ruta | Protección | Descripción |
|---|---|---|---|
| POST | `/api/autenticacion/iniciar` | Anónimo | Inicia sesión y devuelve el token JWT |
| GET | `/api/autenticacion/me` | `[Authorize]` | Datos públicos del usuario autenticado (identidad tomada del JWT) |
| POST | `/api/autenticacion/cerrar` | `[Authorize]` | Registra el cierre de sesión en `dbo.Bitacora` (204) |

Pendientes (otras fases): `/api/capas`, `/api/capas/{capa}/geojson`, `/api/capas/{capa}/{id}`,
`/api/busqueda`, `/api/migraciones`.

### Ejemplo de inicio de sesión

```http
POST /api/autenticacion/iniciar
Content-Type: application/json

{ "login": "admin", "password": "Admin123!" }
```

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 1800,
  "usuario": {
    "idUsuario": 1,
    "login": "admin",
    "nombre": "Administrador",
    "roles": ["Administrador"]
  }
}
```

El token contiene los claims `sub` (IdUsuario), `login`, `name`, `role` (uno por rol), `jti`, `iat` y `exp`.

Para consumir un endpoint protegido:

```http
GET /api/autenticacion/me
Authorization: Bearer <accessToken>
```

> El cierre de sesión no revoca el token (no hay lista de revocación todavía):
> el cliente debe eliminar su token de acceso.

## Swagger

Disponible solo en entorno **Development**:

1. Ejecutar la API con `dotnet run --project src\VisorDatosSIG.Api`: el perfil de desarrollo de
   `src/VisorDatosSIG.Api/Properties/launchSettings.json` activa `Development` y escucha en
   `http://localhost:5080` (no requiere `--urls` ni variables de entorno).
2. Abrir `http://localhost:5080/swagger`.
3. Botón **Authorize** → pegar el token (sin la palabra `Bearer`).
4. Probar `GET /api/autenticacion/me` y `POST /api/autenticacion/cerrar`.

## CORS

El visor web es un origen distinto (`http://localhost:5000`) y consume la API directamente desde el
navegador, por lo que la API aplica una política CORS explícita llamada **`VisorWeb`**:

| Aspecto | Valor |
|---|---|
| Orígenes | Se leen de `Cors:AllowedOrigins` (arreglo de configuración) |
| Comodines | No se usan: la API rechaza `"*"` al iniciar y nunca habilita `AllowAnyOrigin` |
| Credenciales | No se habilitan (`DisallowCredentials`): la sesión viaja en el encabezado `Authorization`, no en cookies |
| Encabezados permitidos | `Authorization`, `Content-Type`, `Accept` |
| Métodos permitidos | `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `OPTIONS` |
| Lista vacía | La política no autoriza ningún origen (no hay respaldo con comodines, solo se registra un aviso) |

En desarrollo la lista se define en `Properties/launchSettings.json` (archivo **versionado y sin
secretos**), por lo que no hace falta una copia nueva de `appsettings.Development.json`:

```json
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development",
  "Cors__AllowedOrigins__0": "http://localhost:5000"
}
```

Para agregar más orígenes use `Cors__AllowedOrigins__1`, `Cors__AllowedOrigins__2`, ... o la clave
`Cors:AllowedOrigins` en `appsettings.Development.json` (no versionado). Escriba los orígenes en
minúsculas, sin ruta, sin consulta y sin barra final.

`appsettings.json` (versionado) solo declara la sección vacía `Cors:AllowedOrigins: []`.

Orden del pipeline HTTP: **enrutamiento → CORS → autenticación → autorización → controladores**.
CORS se ejecuta antes de la autenticación para que las solicitudes preflight (`OPTIONS`, sin token)
reciban las cabeceras CORS en lugar de un `401`.

Puertos locales de desarrollo: API `http://localhost:5080`, visor web `http://localhost:5000`.

## Configuración (sin secretos en el repositorio)

La API requiere dos valores que **no** se versionan:

| Clave | Descripción |
|---|---|
| `ConnectionStrings:VisorDatosSIG` | Cadena de conexión a SQL Server |
| `Jwt:Key` | Clave de firma HMAC-SHA256 (mínimo 32 bytes) |

`appsettings.json` contiene solo una plantilla local sin credenciales.
Para desarrollo local use una de estas opciones:

```bash
# 1) User Secrets (recomendado)
dotnet user-secrets init --project src/VisorDatosSIG.Api
dotnet user-secrets set "ConnectionStrings:VisorDatosSIG" "Server=localhost;Database=VisorDatosSIG;Integrated Security=True;TrustServerCertificate=True" --project src/VisorDatosSIG.Api
dotnet user-secrets set "Jwt:Key" "<clave de al menos 32 caracteres>" --project src/VisorDatosSIG.Api

# 2) Variables de entorno
set ConnectionStrings__VisorDatosSIG=...
set Jwt__Key=...
```

También puede usar un `appsettings.Development.json` local (no versionado, ya excluido en `.gitignore`).

`Properties/launchSettings.json` (versionado) solo define la dirección local `http://localhost:5080`
y el entorno `Development`: no contiene cadenas de conexión, claves ni ningún secreto.

Valores por defecto de JWT: `Issuer = VisorDatosSIG.Api`, `Audience = VisorDatosSIG.Clientes`,
`ExpirationMinutes = 30`.

## Esquema de contraseñas

PBKDF2-SHA256 con salt aleatoria de 32 bytes, 100000 iteraciones y hash de 32 bytes
(`Rfc2898DeriveBytes.Pbkdf2`), comparado en tiempo constante con
`CryptographicOperations.FixedTimeEquals`. No se utilizan Argon2 ni BCrypt y las
contraseñas nunca se registran ni se persisten en texto plano.

## Bitácora

`dbo.Bitacora` registra, en el módulo `AUTENTICACION`:

- `INICIO_SESION` / `EXITOSO` (incluye IdUsuario e IP de origen)
- `INICIO_SESION` / `FALLIDO` (IdUsuario NULL cuando el login no existe)
- `CIERRE_SESION` / `EXITOSO`

Nunca se registran contraseñas, hashes, salts, tokens ni secretos.

## Reglas de seguridad

- Todo el acceso a SQL Server es parametrizado (Microsoft.Data.SqlClient / ADO.NET, sin EF Core).
- La identidad de los endpoints protegidos se obtiene exclusivamente del JWT.
- Los errores internos se devuelven como `ProblemDetails` genérico (sin trazas).
- No se exponen credenciales, cadenas de conexión ni trazas internas.
