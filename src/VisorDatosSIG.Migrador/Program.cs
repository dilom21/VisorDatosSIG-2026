using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Shapefiles;
using VisorDatosSIG.Infrastructure.Validation;
using VisorDatosSIG.Migrador.Forms;
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

        // Se usa un alias porque el espacio de nombres VisorDatosSIG.Application
        // oculta al tipo Application de Windows Forms dentro de este archivo.
        WinFormsApplication.Run(new MigradorForm(shapefileReader, shapefileValidator));
    }
}
