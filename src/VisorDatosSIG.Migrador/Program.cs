using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Shapefiles;
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
        // Application define los contratos (IShapefileLayerDetector / IShapefileReader)
        // e Infrastructure aporta la lectura física de los archivos del shapefile.
        IShapefileLayerDetector layerDetector = new ShapefileLayerDetector();
        IShapefileReader shapefileReader = new ShapefileReader(layerDetector);

        // Se usa un alias porque el espacio de nombres VisorDatosSIG.Application
        // oculta al tipo Application de Windows Forms dentro de este archivo.
        WinFormsApplication.Run(new MigradorForm(shapefileReader));
    }
}
