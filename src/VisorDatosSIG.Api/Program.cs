using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using VisorDatosSIG.Api.Configuration;
using VisorDatosSIG.Api.Middleware;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Authentication;
using VisorDatosSIG.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuración de JWT (Issuer, Audience, Key, ExpirationMinutes).
// La clave NO se versiona: se define con User Secrets, variables de entorno
// (Jwt__Key) o appsettings.Development.json (no versionado).
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

var errorJwt = jwtSettings.Validar();
if (errorJwt is not null)
{
    throw new InvalidOperationException(
        $"{errorJwt} Configure la sección 'Jwt' con User Secrets " +
        "(dotnet user-secrets set \"Jwt:Key\" \"<clave de al menos 32 bytes>\"), " +
        "variables de entorno (Jwt__Key) o appsettings.Development.json (no versionado).");
}

// ---------------------------------------------------------------------------
// CORS: los orígenes autorizados se leen de Cors:AllowedOrigins (arreglo).
// En desarrollo se definen en Properties/launchSettings.json mediante las
// variables Cors__AllowedOrigins__0, Cors__AllowedOrigins__1, ... (archivo
// versionado y sin secretos), o en appsettings.Development.json (no versionado).
// La API nunca habilita AllowAnyOrigin ni credenciales: la sesión viaja en el
// encabezado Authorization, no en cookies.
// ---------------------------------------------------------------------------
var corsSettings = CorsSettings.Desde(builder.Configuration);

var errorCors = corsSettings.Validar();
if (errorCors is not null)
{
    throw new InvalidOperationException(errorCors);
}

// ---------------------------------------------------------------------------
// Servicios
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Acceso a SQL Server (la cadena de conexión llega desde la configuración).
var cadenaConexion = builder.Configuration.GetConnectionString(SqlConnectionFactory.ConnectionStringName) ?? string.Empty;
builder.Services.AddSingleton(new SqlConnectionFactory(cadenaConexion));

// Autenticación y bitácora.
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IBitacoraRepository, BitacoraRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// Autenticación JWT Bearer.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Se conservan los nombres de claim definidos en el token (sub, login, name, role).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtTokenService.ClaimNombre,
            RoleClaimType = JwtTokenService.ClaimRol
        };
    });

builder.Services.AddAuthorization();

// Política CORS exclusiva para el visor web: únicamente los orígenes configurados.
// No se usa AllowAnyOrigin ni AllowCredentials.
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsSettings.NombrePolitica, politica =>
    {
        if (!corsSettings.TieneOrigenes)
        {
            // Sin orígenes configurados la política no autoriza ninguno:
            // no hay respaldo con comodines.
            return;
        }

        politica
            .WithOrigins(corsSettings.AllowedOrigins)
            .WithHeaders(CorsSettings.EncabezadosPermitidos)
            .WithMethods(CorsSettings.MetodosPermitidos)
            .DisallowCredentials();
    });
});

// Swagger / OpenAPI con soporte de Bearer.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VisorDatosSIG API",
        Version = "v1",
        Description = "Servicios HTTP del VisorDatosSIG 2026: autenticación y, más adelante, capas, consultas y migraciones."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Token JWT obtenido en POST /api/autenticacion/iniciar. Escriba únicamente el token: Swagger agrega el prefijo 'Bearer '.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", documento)] = []
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware (el orden es obligatorio: autenticación antes de autorización,
// y ambas antes del mapeo de controladores).
// ---------------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "VisorDatosSIG API v1");
        options.DocumentTitle = "VisorDatosSIG API";
    });
}

app.UseHttpsRedirection();

// ---------------------------------------------------------------------------
// El orden del pipeline es obligatorio:
//   enrutamiento -> CORS -> autenticación -> autorización -> controladores.
// UseRouting se declara explícitamente para fijar ese orden y UseCors se ubica
// antes de la autenticación para que las solicitudes preflight (OPTIONS, sin
// token) reciban las cabeceras CORS en lugar de un 401.
// ---------------------------------------------------------------------------
app.UseRouting();
app.UseCors(CorsSettings.NombrePolitica);
app.UseAuthentication();
app.UseAuthorization();

if (!corsSettings.TieneOrigenes)
{
    app.Logger.LogWarning(
        "CORS: no hay orígenes configurados en {Clave}; ningún origen de navegador podrá consumir la API " +
        "(no se habilita AllowAnyOrigin como respaldo). En desarrollo defina Cors__AllowedOrigins__0 " +
        "en Properties/launchSettings.json.",
        CorsSettings.ClaveAllowedOrigins);
}

app.MapControllers();

app.Run();
