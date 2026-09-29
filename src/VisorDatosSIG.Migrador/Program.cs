using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Migration;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Infrastructure.Shapefiles;
using VisorDatosSIG.Infrastructure.Validation;
using VisorDatosSIG.Migrador.Forms;
using Microsoft.Extensions.Configuration;
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
            .Build();

        var connectionString = configuration.GetConnectionString("VisorDatosSIG")
            ?? throw new InvalidOperationException("No se configuró ConnectionStrings:VisorDatosSIG.");
        var batchSize = int.TryParse(configuration["Migration:BatchSize"], out var configuredBatchSize)
            ? configuredBatchSize
            : 500;

        // Composición de dependencias sin contenedor externo:
        // Application define los contratos (detector, lector y validador)
        // e Infrastructure aporta la lectura y el análisis con NetTopologySuite.
        IShapefileLayerDetector layerDetector = new ShapefileLayerDetector();
        IShapefileReader shapefileReader = new ShapefileReader(layerDetector);
        IShapefileValidator shapefileValidator = new ShapefileValidator();
        var connectionFactory = new SqlServerConnectionFactory(connectionString);
        ISqlServerConnectionProbe connectionProbe = new SqlServerConnectionProbe(connectionFactory);
        var migrationWriter = new SqlMigrationWriter(connectionFactory);
        IMigrationService migrationService = new MigrationService(shapefileReader, shapefileValidator, migrationWriter);

        // Se usa un alias porque el espacio de nombres VisorDatosSIG.Application
        // oculta al tipo Application de Windows Forms dentro de este archivo.
        WinFormsApplication.Run(new MigradorForm(
            shapefileReader,
            shapefileValidator,
            connectionProbe,
            migrationService,
            batchSize));
    }
}
