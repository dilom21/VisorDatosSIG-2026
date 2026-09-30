using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Avatar circular con las iniciales del usuario sobre un degradado violeta → azul.
/// </summary>
internal sealed class AvatarInicial : Control
{
    private const float FactorFuente = 0.45F;

    private string _iniciales = "?";

    /// <summary>Crea el avatar con el estilo de VisorDatosSIG.</summary>
    public AvatarInicial()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Size = new Size(38, 38);
        AccessibleRole = AccessibleRole.Graphic;
    }

    /// <summary>Iniciales que se dibujan dentro del círculo.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Iniciales
    {
        get => _iniciales;
        set
        {
            var normalizadas = string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().ToUpperInvariant();
            if (_iniciales == normalizadas)
            {
                return;
            }

            _iniciales = normalizadas;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e) =>
        Dibujar(e.Graphics, ClientRectangle, _iniciales, EstiloUI.GradienteInicio, EstiloUI.GradienteFin);

    /// <summary>
    /// Dibuja un avatar circular en el área indicada (se reutiliza en el menú y en la cabecera).
    /// </summary>
    public static void Dibujar(Graphics graficos, Rectangle area, string iniciales, Color inicio, Color fin)
    {
        ArgumentNullException.ThrowIfNull(graficos);

        var lado = Math.Min(area.Width, area.Height);
        if (lado <= 2)
        {
            return;
        }

        var anterior = graficos.SmoothingMode;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        var circulo = new RectangleF(
            area.Left + ((area.Width - lado) / 2F),
            area.Top + ((area.Height - lado) / 2F),
            lado,
            lado);

        using (var pincel = new LinearGradientBrush(circulo, inicio, fin, LinearGradientMode.ForwardDiagonal))
        {
            graficos.FillEllipse(pincel, circulo);
        }

        const TextFormatFlags banderas =
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine
            | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;

        using var fuente = new Font("Segoe UI", Math.Max(7F, lado * FactorFuente), FontStyle.Bold);
        TextRenderer.DrawText(graficos, iniciales, fuente, area, Color.White, banderas);

        graficos.SmoothingMode = anterior;
    }
}
