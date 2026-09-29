using Microsoft.Extensions.Configuration;
using VisorDatosSIG.Api.Configuration;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas de la configuración de CORS de la API (política VisorWeb).
/// </summary>
public sealed class CorsSettingsTests
{
    private static CorsSettings Crear(params string[] origenes)
    {
        var valores = new Dictionary<string, string?>();
        for (var i = 0; i < origenes.Length; i++)
        {
            valores[$"{CorsSettings.ClaveAllowedOrigins}:{i}"] = origenes[i];
        }

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build();

        return CorsSettings.Desde(configuracion);
    }

    [Fact(DisplayName = "CORS: la política se llama VisorWeb y se lee Cors:AllowedOrigins")]
    public void PoliticaYClaveDeConfiguracion()
    {
        Assert.Equal("VisorWeb", CorsSettings.NombrePolitica);
        Assert.Equal("Cors:AllowedOrigins", CorsSettings.ClaveAllowedOrigins);
    }

    [Fact(DisplayName = "CORS: lee los orígenes desde la configuración y los normaliza")]
    public void LeeYNormalizaLosOrigenes()
    {
        var ajustes = Crear("http://localhost:5000", " http://localhost:5000/ ", "https://visor.ejemplo.local");

        Assert.True(ajustes.TieneOrigenes);
        Assert.Equal(2, ajustes.AllowedOrigins.Length);
        Assert.Contains("http://localhost:5000", ajustes.AllowedOrigins);
        Assert.Contains("https://visor.ejemplo.local", ajustes.AllowedOrigins);
        Assert.Null(ajustes.Validar());
    }

    [Fact(DisplayName = "CORS: una lista vacía no autoriza ningún origen (sin respaldo con comodines)")]
    public void ListaVaciaNoAutorizaNingunOrigen()
    {
        var ajustes = Crear();

        Assert.False(ajustes.TieneOrigenes);
        Assert.Empty(ajustes.AllowedOrigins);
        Assert.Null(ajustes.Validar());
    }

    [Fact(DisplayName = "CORS: rechaza el comodín \"*\"")]
    public void RechazaComodin()
    {
        var ajustes = Crear("*");

        Assert.NotNull(ajustes.Validar());
        Assert.Contains("comodines", ajustes.Validar()!);
    }

    [Fact(DisplayName = "CORS: rechaza orígenes con ruta, sin esquema o con otro esquema")]
    public void RechazaOrigenesInvalidos()
    {
        Assert.NotNull(Crear("http://localhost:5000/visor").Validar());
        Assert.NotNull(Crear("localhost:5000").Validar());
        Assert.NotNull(Crear("ftp://localhost:5000").Validar());
    }

    [Fact(DisplayName = "CORS: descarta los valores vacíos sin reportar error")]
    public void DescartaValoresVacios()
    {
        var ajustes = Crear(string.Empty, "   ");

        Assert.False(ajustes.TieneOrigenes);
        Assert.Null(ajustes.Validar());
    }

    [Fact(DisplayName = "CORS: los encabezados incluyen Authorization y los métodos necesarios")]
    public void EncabezadosYMetodosPermitidos()
    {
        Assert.Contains("Authorization", CorsSettings.EncabezadosPermitidos);
        Assert.Contains("Content-Type", CorsSettings.EncabezadosPermitidos);
        Assert.Contains("Accept", CorsSettings.EncabezadosPermitidos);

        Assert.Contains("GET", CorsSettings.MetodosPermitidos);
        Assert.Contains("POST", CorsSettings.MetodosPermitidos);
        Assert.Contains("OPTIONS", CorsSettings.MetodosPermitidos);
        Assert.DoesNotContain("*", CorsSettings.EncabezadosPermitidos);
        Assert.DoesNotContain("*", CorsSettings.MetodosPermitidos);
    }
}
