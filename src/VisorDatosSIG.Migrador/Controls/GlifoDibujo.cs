using System.Drawing;
using System.Drawing.Drawing2D;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Iconografía de línea del Migrador.
/// </summary>
internal enum GlifoIcono
{
    /// <summary>Persona (campo de usuario, «Mi cuenta»).</summary>
    Usuario,

    /// <summary>Candado (campo de contraseña).</summary>
    Candado,

    /// <summary>Ojo abierto (mostrar contraseña).</summary>
    Ojo,

    /// <summary>Ojo tachado (ocultar contraseña).</summary>
    OjoTachado,

    /// <summary>Salir (cerrar sesión).</summary>
    Salir
}

/// <summary>
/// Dibuja los iconos de línea de la interfaz de sesión con GDI+.
/// </summary>
/// <remarks>
/// Se dibujan formas en lugar de usar fuentes de iconos o imágenes: así el resultado es idéntico
/// y nítido en cualquier equipo, sin dependencias externas ni recursos que versionar.
/// </remarks>
internal static class GlifoDibujo
{
    /// <summary>
    /// Dibuja un icono de línea dentro del área indicada.
    /// </summary>
    /// <param name="graficos">Superficie de dibujo.</param>
    /// <param name="glifo">Icono a dibujar.</param>
    /// <param name="area">Área disponible para el icono.</param>
    /// <param name="color">Color del trazo.</param>
    /// <param name="grosor">Grosor del trazo en píxeles.</param>
    public static void Dibujar(Graphics graficos, GlifoIcono glifo, RectangleF area, Color color, float grosor = 1.5F)
    {
        ArgumentNullException.ThrowIfNull(graficos);

        if (area.Width <= 1F || area.Height <= 1F)
        {
            return;
        }

        var anterior = graficos.SmoothingMode;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        using var lapiz = new Pen(color, grosor)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (glifo)
        {
            case GlifoIcono.Usuario:
                DibujarUsuario(graficos, lapiz, area);
                break;

            case GlifoIcono.Candado:
                DibujarCandado(graficos, lapiz, area);
                break;

            case GlifoIcono.Ojo:
                DibujarOjo(graficos, lapiz, area, color, tachado: false);
                break;

            case GlifoIcono.OjoTachado:
                DibujarOjo(graficos, lapiz, area, color, tachado: true);
                break;

            default:
                DibujarSalir(graficos, lapiz, area);
                break;
        }

        graficos.SmoothingMode = anterior;
    }

    /// <summary>
    /// Dibuja una flecha horizontal con punta redondeada (usada en el botón principal).
    /// </summary>
    public static void DibujarFlecha(Graphics graficos, RectangleF area, Color color, float grosor = 1.8F)
    {
        ArgumentNullException.ThrowIfNull(graficos);

        if (area.Width <= 1F || area.Height <= 1F)
        {
            return;
        }

        var anterior = graficos.SmoothingMode;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        using var lapiz = new Pen(color, grosor)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        var centroY = area.Top + (area.Height / 2F);
        graficos.DrawLine(lapiz, area.Left, centroY, area.Right - (area.Width * 0.25F), centroY);
        graficos.DrawLines(
            lapiz,
            [
                new PointF(area.Right - (area.Width * 0.42F), centroY - (area.Height * 0.28F)),
                new PointF(area.Right - (area.Width * 0.02F), centroY),
                new PointF(area.Right - (area.Width * 0.42F), centroY + (area.Height * 0.28F))
            ]);

        graficos.SmoothingMode = anterior;
    }

    private static void DibujarUsuario(Graphics graficos, Pen lapiz, RectangleF area)
    {
        var centroX = area.Left + (area.Width / 2F);
        var radioCabeza = area.Width * 0.19F;
        var centroCabezaY = area.Top + (area.Height * 0.30F);

        graficos.DrawEllipse(
            lapiz,
            centroX - radioCabeza,
            centroCabezaY - radioCabeza,
            radioCabeza * 2F,
            radioCabeza * 2F);

        var hombros = new RectangleF(
            centroX - (area.Width * 0.34F),
            centroCabezaY + (radioCabeza * 1.05F),
            area.Width * 0.68F,
            area.Height * 0.62F);

        graficos.DrawArc(lapiz, hombros, 200F, 140F);
    }

    private static void DibujarCandado(Graphics graficos, Pen lapiz, RectangleF area)
    {
        var cuerpo = new RectangleF(
            area.Left + (area.Width * 0.16F),
            area.Top + (area.Height * 0.44F),
            area.Width * 0.68F,
            area.Height * 0.46F);

        using (var ruta = EstiloUI.RutaRedondeada(cuerpo, area.Width * 0.14F))
        {
            graficos.DrawPath(lapiz, ruta);
        }

        var arco = new RectangleF(
            area.Left + (area.Width * 0.30F),
            area.Top + (area.Height * 0.09F),
            area.Width * 0.40F,
            area.Height * 0.52F);

        graficos.DrawArc(lapiz, arco, 180F, 180F);
    }

    private static void DibujarOjo(Graphics graficos, Pen lapiz, RectangleF area, Color color, bool tachado)
    {
        var alto = area.Height * 0.62F;
        var contorno = new RectangleF(area.Left, area.Top + ((area.Height - alto) / 2F), area.Width, alto);

        graficos.DrawEllipse(lapiz, contorno);

        var radioPupila = area.Width * 0.13F;
        var centroX = area.Left + (area.Width / 2F);
        var centroY = area.Top + (area.Height / 2F);

        using (var relleno = new SolidBrush(color))
        {
            graficos.FillEllipse(relleno, centroX - radioPupila, centroY - radioPupila, radioPupila * 2F, radioPupila * 2F);
        }

        if (tachado)
        {
            graficos.DrawLine(
                lapiz,
                area.Left + (area.Width * 0.10F),
                area.Bottom - (area.Height * 0.10F),
                area.Right - (area.Width * 0.10F),
                area.Top + (area.Height * 0.10F));
        }
    }

    private static void DibujarSalir(Graphics graficos, Pen lapiz, RectangleF area)
    {
        var puerta = new RectangleF(
            area.Left + (area.Width * 0.06F),
            area.Top + (area.Height * 0.10F),
            area.Width * 0.50F,
            area.Height * 0.80F);

        graficos.DrawLines(
            lapiz,
            [
                new PointF(puerta.Right, puerta.Top),
                new PointF(puerta.Left, puerta.Top),
                new PointF(puerta.Left, puerta.Bottom),
                new PointF(puerta.Right, puerta.Bottom)
            ]);

        var centroY = area.Top + (area.Height / 2F);
        graficos.DrawLine(lapiz, area.Left + (area.Width * 0.42F), centroY, area.Right - (area.Width * 0.12F), centroY);
        graficos.DrawLines(
            lapiz,
            [
                new PointF(area.Right - (area.Width * 0.30F), centroY - (area.Height * 0.17F)),
                new PointF(area.Right - (area.Width * 0.12F), centroY),
                new PointF(area.Right - (area.Width * 0.30F), centroY + (area.Height * 0.17F))
            ]);
    }
}
