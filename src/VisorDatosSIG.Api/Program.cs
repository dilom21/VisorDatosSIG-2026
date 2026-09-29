using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
