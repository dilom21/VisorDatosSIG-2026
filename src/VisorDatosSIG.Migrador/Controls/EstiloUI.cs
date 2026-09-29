using System.Drawing;
using System.Drawing.Drawing2D;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Paleta, tipografías y utilidades de dibujo compartidas por la interfaz de sesión del Migrador.
/// </summary>
/// <remarks>
/// Los controles se dibujan con GDI+ sobre controles propios ligeros: no se agregan paquetes
/// de interfaz externos. La identidad visual usa violeta como color primario y azul como
/// secundario, coherente con la referencia de diseño aportada para el inicio de sesión.
/// </remarks>
internal static class EstiloUI
{
    // Superficies.
    public static readonly Color Fondo = Color.FromArgb(245, 246, 250);
    public static readonly Color Tarjeta = Color.White;
    public static readonly Color BordeTarjeta = Color.FromArgb(227, 213, 250);
    public static readonly Color Sombra = Color.FromArgb(26, 32, 51);

    // Identidad (violeta → azul).
    public static readonly Color Violeta = Color.FromArgb(124, 58, 237);
    public static readonly Color VioletaSuave = Color.FromArgb(245, 240, 255);
    public static readonly Color Azul = Color.FromArgb(47, 128, 237);
    public static readonly Color GradienteInicio = Color.FromArgb(181, 23, 240);
    public static readonly Color GradienteFin = Color.FromArgb(47, 128, 237);

    // Textos.
    public static readonly Color TextoPrincipal = Color.FromArgb(31, 36, 48);
    public static readonly Color TextoSecundario = Color.FromArgb(108, 117, 132);
    public static readonly Color TextoTenue = Color.FromArgb(158, 165, 177);

    // Estados.
    public static readonly Color Error = Color.FromArgb(200, 62, 62);
    public static readonly Color Exito = Color.FromArgb(24, 128, 74);

    // Campos de entrada.
    public static readonly Color CampoFondo = Color.White;
    public static readonly Color CampoBorde = Color.FromArgb(222, 227, 234);
    public static readonly Color CampoBordeFoco = Violeta;

    // Cabecera del Migrador (azul institucional ya usado en las fases 1 y 2).
    public static readonly Color FondoCabecera = Color.FromArgb(31, 59, 91);
    public static readonly Color TextoCabeceraSuave = Color.FromArgb(203, 216, 231);
    public static readonly Color ChipFondo = Color.FromArgb(42, 78, 116);
    public static readonly Color ChipFondoHover = Color.FromArgb(54, 96, 141);

    // Tipografías.
    public static readonly Font FuenteTitulo = new("Segoe UI", 19F, FontStyle.Bold);
    public static readonly Font FuenteBienvenida = new("Segoe UI", 12.5F, FontStyle.Bold);
    public static readonly Font FuenteCuerpo = new("Segoe UI", 9.75F);
    public static readonly Font FuenteCuerpoDestacado = new("Segoe UI", 9.75F, FontStyle.Bold);
    public static readonly Font FuenteEtiqueta = new("Segoe UI", 9F, FontStyle.Bold);
    public static readonly Font FuenteBoton = new("Segoe UI", 10.5F, FontStyle.Bold);
    public static readonly Font FuenteBotonPequeno = new("Segoe UI", 9F, FontStyle.Bold);
    public static readonly Font FuenteMenor = new("Segoe UI", 8.25F);

    /// <summary>
    /// Crea la ruta de un rectángulo con esquinas redondeadas.
    /// </summary>
    /// <param name="area">Área del rectángulo.</param>
    /// <param name="radio">Radio de las esquinas en píxeles.</param>
    public static GraphicsPath RutaRedondeada(RectangleF area, float radio)
    {
        var ruta = new GraphicsPath();

        if (area.Width <= 0 || area.Height <= 0)
        {
            return ruta;
        }

        var limite = Math.Min(area.Width, area.Height) / 2F;
        radio = Math.Max(0F, Math.Min(radio, limite));

        if (radio < 0.5F)
        {
            ruta.AddRectangle(area);
            return ruta;
        }

        var diametro = radio * 2F;
        ruta.AddArc(area.Left, area.Top, diametro, diametro, 180F, 90F);
        ruta.AddArc(area.Right - diametro, area.Top, diametro, diametro, 270F, 90F);
        ruta.AddArc(area.Right - diametro, area.Bottom - diametro, diametro, diametro, 0F, 90F);
        ruta.AddArc(area.Left, area.Bottom - diametro, diametro, diametro, 90F, 90F);
        ruta.CloseFigure();
        return ruta;
    }

    /// <summary>
    /// Mezcla dos colores: <paramref name="factor"/> = 0 devuelve el origen y 1 el destino.
    /// </summary>
    public static Color Mezclar(Color origen, Color destino, double factor)
    {
        var proporcion = Math.Clamp(factor, 0D, 1D);

        return Color.FromArgb(
            origen.A,
            (int)Math.Round(origen.R + ((destino.R - origen.R) * proporcion)),
            (int)Math.Round(origen.G + ((destino.G - origen.G) * proporcion)),
            (int)Math.Round(origen.B + ((destino.B - origen.B) * proporcion)));
    }

    /// <summary>Devuelve el mismo color con otra transparencia.</summary>
    public static Color ConAlfa(Color color, int alfa) =>
        Color.FromArgb(Math.Clamp(alfa, 0, 255), color);
}
