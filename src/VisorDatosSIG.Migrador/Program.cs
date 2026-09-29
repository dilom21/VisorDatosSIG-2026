using VisorDatosSIG.Application.Interfaces;
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

        // Composición de dependencias sin contenedor externo:
        // Application define los contratos (detector, lector y validador)
        // e Infrastructure aporta la lectura y el análisis con NetTopologySuite.
        IShapefileLayerDetector layerDetector = new ShapefileLayerDetector();
        IShapefileReader shapefileReader = new ShapefileReader(layerDetector);
        IShapefileValidator shapefileValidator = new ShapefileValidator();

        // El Migrador solo conoce la API: nunca la cadena de conexión ni la clave JWT.
        // La dirección de la API se centraliza en ApiSettings (variable de entorno
        // VISORDATOSSIG_API_URL) y el HttpClient se crea una única vez por aplicación.
        var ajustesApi = ApiSettings.CrearDesdeEntorno();
        using var clienteHttp = ajustesApi.CrearClienteHttp();
        var clienteAutenticacion = new AuthenticationApiClient(clienteHttp);

        // Sesión en memoria: el token se pierde al cerrar la aplicación.
        var sesion = new UserSession();

        // Se usa un alias porque el espacio de nombres VisorDatosSIG.Application
        // oculta al tipo Application de Windows Forms dentro de este archivo.
        WinFormsApplication.Run(new MigradorForm(shapefileReader, shapefileValidator, clienteAutenticacion, sesion));
    }
}
