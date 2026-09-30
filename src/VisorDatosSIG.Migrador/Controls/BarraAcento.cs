using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Barra decorativa con degradado violeta → azul, usada como acento bajo los títulos de los
/// formularios de sesión.
/// </summary>
internal sealed class BarraAcento : Control
{
    /// <summary>Crea la barra con el tamaño y el estilo predeterminados.</summary>
    public BarraAcento()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Size = new Size(76, 6);
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 2 || Height <= 2)
        {
            return;
        }

        var graficos = e.Graphics;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        var area = new RectangleF(0F, 0.5F, Width, Math.Max(2F, Height - 1F));

        using var ruta = EstiloUI.RutaRedondeada(area, area.Height / 2F);
        using var pincel = new LinearGradientBrush(area, EstiloUI.GradienteInicio, EstiloUI.GradienteFin, LinearGradientMode.Horizontal);
        graficos.FillPath(pincel, ruta);
    }
}
