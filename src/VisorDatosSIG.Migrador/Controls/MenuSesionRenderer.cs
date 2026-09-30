using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Colores del menú de sesión: superficie blanca, borde violeta suave y resaltado violeta claro.
/// </summary>
internal sealed class MenuSesionColorTable : ProfessionalColorTable
{
    private static readonly Color Separador = Color.FromArgb(236, 238, 243);
    private static readonly Color Presionado = Color.FromArgb(234, 224, 253);

    /// <inheritdoc />
    public override Color ToolStripDropDownBackground => Color.White;

    /// <inheritdoc />
    public override Color ImageMarginGradientBegin => Color.White;

    /// <inheritdoc />
    public override Color ImageMarginGradientMiddle => Color.White;

    /// <inheritdoc />
    public override Color ImageMarginGradientEnd => Color.White;

    /// <inheritdoc />
    public override Color MenuBorder => EstiloUI.BordeTarjeta;

    /// <inheritdoc />
    public override Color MenuItemBorder => Color.Transparent;

    /// <inheritdoc />
    public override Color MenuItemSelected => EstiloUI.VioletaSuave;

    /// <inheritdoc />
    public override Color MenuItemSelectedGradientBegin => EstiloUI.VioletaSuave;

    /// <inheritdoc />
    public override Color MenuItemSelectedGradientEnd => EstiloUI.VioletaSuave;

    /// <inheritdoc />
    public override Color MenuItemPressedGradientBegin => Presionado;

    /// <inheritdoc />
    public override Color MenuItemPressedGradientEnd => Presionado;

    /// <inheritdoc />
    public override Color SeparatorDark => Separador;

    /// <inheritdoc />
    public override Color SeparatorLight => Separador;
}

/// <summary>
/// Dibuja el menú de sesión con la apariencia de VisorDatosSIG en lugar del gris estándar.
/// </summary>
internal sealed class MenuSesionRenderer : ToolStripProfessionalRenderer
{
    /// <summary>Crea el renderizador con la tabla de colores del menú.</summary>
    public MenuSesionRenderer()
        : base(new MenuSesionColorTable()) =>
        RoundedEdges = true;

    /// <inheritdoc />
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is null)
        {
            return;
        }

        e.Graphics.Clear(Color.White);
    }

    /// <inheritdoc />
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is not ToolStripDropDown)
        {
            return;
        }

        var anterior = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var area = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var ruta = EstiloUI.RutaRedondeada(area, 10F);
        using var lapiz = new Pen(EstiloUI.BordeTarjeta, 1F);
        e.Graphics.DrawPath(lapiz, ruta);

        e.Graphics.SmoothingMode = anterior;
    }

    /// <inheritdoc />
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        // El encabezado del usuario no se resalta: solo las opciones seleccionables.
        if (e.Item is not { Selected: true, Enabled: true })
        {
            return;
        }

        var anterior = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var area = new RectangleF(2F, 0F, Math.Max(2F, e.Item.Width - 4F), e.Item.Height);
        using var ruta = EstiloUI.RutaRedondeada(area, 8F);
        using var relleno = new SolidBrush(e.Item.Pressed ? Color.FromArgb(234, 224, 253) : EstiloUI.VioletaSuave);
        e.Graphics.FillPath(relleno, ruta);

        e.Graphics.SmoothingMode = anterior;
    }

    /// <inheritdoc />
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item.Enabled)
        {
            e.TextColor = EstiloUI.TextoPrincipal;
        }

        base.OnRenderItemText(e);
    }

    /// <inheritdoc />
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var lapiz = new Pen(Color.FromArgb(236, 238, 243), 1F);
        var centroY = e.Item.Height / 2;
        e.Graphics.DrawLine(lapiz, 10, centroY, Math.Max(11, e.Item.Width - 10), centroY);
    }
}
