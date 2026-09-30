using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Tarjeta blanca de esquinas redondeadas con borde suave y sombra discreta.
/// </summary>
/// <remarks>
/// El contenido se agrega con <c>Dock</c> o <c>Anchor</c> y queda separado del borde por el
/// relleno de la tarjeta, por lo que las esquinas redondeadas nunca recortan contenido.
/// </remarks>
internal sealed class CardPanel : Panel
{
    private const int MargenSombra = 6;

    /// <summary>Crea la tarjeta con el estilo visual de VisorDatosSIG.</summary>
    public CardPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        ResizeRedraw = true;
        Padding = new Padding(30, 28, 26, 24);
    }

    /// <summary>Radio de las esquinas.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radio { get; set; } = 18;

    /// <summary>Color del borde.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ColorBorde { get; set; } = EstiloUI.BordeTarjeta;

    /// <summary>Color de relleno de la tarjeta.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ColorRelleno { get; set; } = EstiloUI.Tarjeta;

    /// <summary>Indica si se dibuja la sombra.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MostrarSombra { get; set; } = true;

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graficos = e.Graphics;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        var area = new RectangleF(1F, 1F, Width - MargenSombra - 1F, Height - MargenSombra - 1F);
        if (area.Width <= 4F || area.Height <= 4F)
        {
            base.OnPaint(e);
            return;
        }

        DibujarSombra(graficos, area);

        using var ruta = EstiloUI.RutaRedondeada(area, Radio);
        using (var relleno = new SolidBrush(ColorRelleno))
        {
            graficos.FillPath(relleno, ruta);
        }

        using var lapiz = new Pen(ColorBorde, 1.2F);
        graficos.DrawPath(lapiz, ruta);
    }

    private void DibujarSombra(Graphics graficos, RectangleF area)
    {
        if (!MostrarSombra)
        {
            return;
        }

        for (var capa = 6; capa >= 1; capa--)
        {
            var extendida = new RectangleF(
                area.Left - (capa * 0.4F),
                area.Top + (capa * 0.5F),
                area.Width + capa,
                area.Height + capa);

            using var ruta = EstiloUI.RutaRedondeada(extendida, Radio + capa);
            using var pincel = new SolidBrush(EstiloUI.ConAlfa(EstiloUI.Sombra, 7));
            graficos.FillPath(pincel, ruta);
        }
    }
}
