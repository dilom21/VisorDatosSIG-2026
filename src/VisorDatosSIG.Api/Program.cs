using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Cadena de conexión y factoría SQL Server 2022
var connectionString = builder.Configuration.GetConnectionString("VisorDatosSIG")
    ?? "Server=localhost;Database=VisorDatosSIG;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddSingleton(new SqlServerConnectionFactory(connectionString));

// 2. Registro de repositorios y servicios de Clean Architecture
builder.Services.AddScoped<ICadastreRepository, CadastreRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IBitacoraService, BitacoraService>();

// 3. Controladores y serialización JSON (CamelCase y RFC 7946 GeoJSON)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// 4. Política de CORS abierta para desarrollo del Frontend (React, Vite, Vue, Leaflet, etc.)
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 5. Configuración de Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "VisorDatosSIG - API Web Geoespacial",
        Version = "v1",
        Description = "Backend REST y GeoJSON (RFC 7946) para soporte del Visor Geográfico, Catastro y Auditoría."
    });
});

var app = builder.Build();

// Habilitar Swagger en la raíz (http://localhost:port/)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VisorDatosSIG API v1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("PermitirTodo");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
