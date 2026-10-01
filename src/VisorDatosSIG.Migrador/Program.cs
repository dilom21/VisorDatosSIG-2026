using Microsoft.Extensions.Configuration;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Migration;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Infrastructure.Shapefiles;
using VisorDatosSIG.Infrastructure.Validation;
using VisorDatosSIG.Migrador.Configuration;
using VisorDatosSIG.Migrador.Forms;
using VisorDatosSIG.Migrador.Services;
using VisorDatosSIG.Migrador.Session;
using WinFormsApplication = System.Windows.Forms.Application;

namespace VisorDatosSIG.Migrador;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();

        var connectionString = configuration.GetConnectionString("VisorDatosSIG")
            ?? throw new InvalidOperationException("No se configuró ConnectionStrings:VisorDatosSIG.");
        var batchSize = int.TryParse(configuration["Migration:BatchSize"], out var configuredBatchSize)
            ? configuredBatchSize
            : 500;

        // Composición de dependencias:
        IShapefileLayerDetector layerDetector = new ShapefileLayerDetector();
        IShapefileReader shapefileReader = new ShapefileReader(layerDetector);
        IShapefileValidator shapefileValidator = new ShapefileValidator();
        var connectionFactory = new SqlServerConnectionFactory(connectionString);
        ISqlServerConnectionProbe connectionProbe = new SqlServerConnectionProbe(connectionFactory);
        IBitacoraService bitacoraService = new BitacoraService(connectionFactory);
        var migrationWriter = new SqlMigrationWriter(connectionFactory);
        IMigrationExporter migrationExporter = new MigrationExporter();
        IMigrationService migrationService = new MigrationService(shapefileReader, shapefileValidator, migrationWriter, bitacoraService);

        // Cliente de autenticación contra la API HTTP (Harold):
        var ajustesApi = ApiSettings.CrearDesdeEntorno();
        using var clienteHttp = ajustesApi.CrearClienteHttp();

        // Sesión en memoria:
        var sesion = new UserSession();
        var clienteAutenticacion = new AuthenticationApiClient(clienteHttp, sesion);

        using (var loginForm = new LoginForm(clienteAutenticacion, sesion))
        {
            var dialogResult = loginForm.ShowDialog();
            if (dialogResult != DialogResult.OK || !sesion.IsAuthenticated)
            {
                return;
            }
        }

        WinFormsApplication.Run(new MigradorForm(
            shapefileReader,
            shapefileValidator,
            connectionProbe,
            migrationService,
            migrationWriter,
            migrationExporter,
            bitacoraService,
            clienteAutenticacion,
            sesion,
            batchSize));
    }
}
