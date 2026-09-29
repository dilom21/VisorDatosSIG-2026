using VisorDatosSIG.Migrador.Configuration;

namespace VisorDatosSIG.Migrador.UnitTests;

/// <summary>
/// Pruebas de la configuración de la API: valor predeterminado, variable de entorno y validación.
/// </summary>
public sealed class ApiSettingsTests
{
    [Fact(DisplayName = "Sin variable de entorno se usa la API local predeterminada")]
    public void SinVariableDeEntornoUsaLaUrlPredeterminada()
    {
        var ajustes = ApiSettings.CrearDesdeEntorno(_ => null);

        Assert.Equal(ApiSettings.UrlPredeterminada + "/", ajustes.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(15), ajustes.Timeout);
        Assert.Null(ajustes.Advertencia);
    }

    [Fact(DisplayName = "Una variable vacía o con espacios usa la API predeterminada")]
    public void VariableVaciaUsaLaUrlPredeterminada()
    {
        Assert.Equal(ApiSettings.Predeterminada.BaseUri, ApiSettings.CrearDesdeEntorno(_ => "   ").BaseUri);
        Assert.Equal(ApiSettings.Predeterminada.BaseUri, ApiSettings.CrearDesdeEntorno(_ => "").BaseUri);
    }

    [Theory(DisplayName = "La dirección se normaliza con barra final")]
    [InlineData("http://localhost:5080")]
    [InlineData("http://localhost:5080/")]
    [InlineData("  http://localhost:5080/api  ")]
    [InlineData("https://api.visordatos.example/")]
    public void LaDireccionSeNormalizaConBarraFinal(string valor)
    {
        var ajustes = ApiSettings.CrearDesdeEntorno(variable => variable == ApiSettings.VariableEntornoUrl ? valor : null);

        Assert.Equal(valor.Trim().TrimEnd('/') + "/", ajustes.BaseUrl);
        Assert.Null(ajustes.Advertencia);
        Assert.Equal(valor.Trim().TrimEnd('/') + "/", ajustes.BaseUri.AbsoluteUri);
    }

    [Theory(DisplayName = "Una dirección inválida no impide iniciar la aplicación")]
    [InlineData("no-es-una-url")]
    [InlineData("ftp://servidor/autenticacion")]
    [InlineData("localhost:5080")]
    public void UnaDireccionInvalidaUsaLaUrlPredeterminadaYRegistraAviso(string valor)
    {
        var ajustes = ApiSettings.CrearDesdeEntorno(variable => variable == ApiSettings.VariableEntornoUrl ? valor : null);

        Assert.Equal(ApiSettings.Predeterminada.BaseUri, ajustes.BaseUri);
        Assert.NotNull(ajustes.Advertencia);
        Assert.Contains(ApiSettings.VariableEntornoUrl, ajustes.Advertencia!, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "La normalización acepta http y https y rechaza el resto")]
    public void LaNormalizacionSoloAceptaHttpYHttps()
    {
        Assert.True(ApiSettings.IntentarNormalizar("http://localhost:5080", out var http));
        Assert.Equal("http://localhost:5080/", http.AbsoluteUri);

        Assert.True(ApiSettings.IntentarNormalizar("https://api.visordatos.example:8443", out var https));
        Assert.Equal("https://api.visordatos.example:8443/", https.AbsoluteUri);

        Assert.False(ApiSettings.IntentarNormalizar(null, out _));
        Assert.False(ApiSettings.IntentarNormalizar("   ", out _));
        Assert.False(ApiSettings.IntentarNormalizar("file:///c:/api", out _));
        Assert.False(ApiSettings.IntentarNormalizar("no-es-una-url", out _));
    }

    [Fact(DisplayName = "El HttpClient reutilizable usa la dirección base y el tiempo de espera configurados")]
    public void ElClienteHttpUsaLaDireccionYElTiempoDeEsperaConfigurados()
    {
        var ajustes = ApiSettings.CrearDesdeEntorno(variable => variable == ApiSettings.VariableEntornoUrl ? "http://localhost:5080" : null);

        using var cliente = ajustes.CrearClienteHttp();

        Assert.Equal(ajustes.BaseUri, cliente.BaseAddress);
        Assert.Equal(ajustes.Timeout, cliente.Timeout);
        Assert.Contains(
            cliente.DefaultRequestHeaders.Accept,
            encabezado => encabezado.MediaType == "application/json");
    }

    [Fact(DisplayName = "La variable de entorno se lee por su nombre documentado")]
    public void LaVariableDeEntornoSeLeePorSuNombreDocumentado()
    {
        string? nombreSolicitado = null;

        _ = ApiSettings.CrearDesdeEntorno(variable =>
        {
            nombreSolicitado = variable;
            return null;
        });

        Assert.Equal("VISORDATOSSIG_API_URL", ApiSettings.VariableEntornoUrl);
        Assert.Equal(ApiSettings.VariableEntornoUrl, nombreSolicitado);
    }
}
