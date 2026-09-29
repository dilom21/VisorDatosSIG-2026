using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Identificación del usuario en la cabecera del Migrador: avatar, nombre, login e indicador de menú.
/// </summary>
internal sealed class UserChipButton : Control
{
    private const int DiametroAvatar = 26;
    private const int MargenIzquierdo = 8;
    private const int AnchoIndicador = 22;

    private string _iniciales = "?";
    private string _textoSecundario = string.Empty;
    private bool _sobreElBoton;
    private bool _presionado;

    /// <summary>Crea el chip con el estilo de la cabecera.</summary>
    public UserChipButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.Selectable,
            true);

        BackColor = Color.Transparent;
        ForeColor = Color.White;
        Font = EstiloUI.FuenteCuerpoDestacado;
        Cursor = Cursors.Hand;
        TabStop = true;
        Height = 42;
        AccessibleRole = AccessibleRole.PushButton;
        AjustarTamano();
    }

    /// <summary>Iniciales del avatar.</summary>
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

    /// <summary>Texto secundario discreto (normalmente el login).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TextoSecundario
    {
        get => _textoSecundario;
        set
        {
            var nuevo = value ?? string.Empty;
            if (_textoSecundario == nuevo)
            {
                return;
            }

            _textoSecundario = nuevo;
            AjustarTamano();
            Invalidate();
        }
    }

    /// <summary>Recalcula el ancho del chip a partir del nombre y del login.</summary>
    public void AjustarTamano()
    {
        var anchoNombre = Medir(Text, Font);
        var anchoLogin = string.IsNullOrEmpty(_textoSecundario) ? 0 : Medir(_textoSecundario, EstiloUI.FuenteMenor);

        Width = MargenIzquierdo + DiametroAvatar + 10 + Math.Max(anchoNombre, anchoLogin) + AnchoIndicador + 8;
        Height = 42;
    }

    /// <summary>Ejecuta el clic del chip desde el teclado.</summary>
    public void PerformClick()
    {
        if (!Enabled)
        {
            return;
        }

        OnClick(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AjustarTamano();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graficos = e.Graphics;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        var area = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
        using var ruta = EstiloUI.RutaRedondeada(area, Height / 2F);

        var fondo = _presionado
            ? EstiloUI.Mezclar(EstiloUI.ChipFondoHover, Color.Black, 0.12)
            : _sobreElBoton ? EstiloUI.ChipFondoHover : EstiloUI.ChipFondo;

        using (var relleno = new SolidBrush(fondo))
        {
            graficos.FillPath(relleno, ruta);
        }

        using (var lapiz = new Pen(EstiloUI.ConAlfa(Color.White, 48), 1F))
        {
            graficos.DrawPath(lapiz, ruta);
        }

        var areaAvatar = new Rectangle(
            MargenIzquierdo,
            (Height - DiametroAvatar) / 2,
            DiametroAvatar,
            DiametroAvatar);

        AvatarInicial.Dibujar(graficos, areaAvatar, _iniciales, EstiloUI.GradienteInicio, EstiloUI.GradienteFin);

        DibujarTextos(graficos);
        DibujarIndicador(graficos, new RectangleF(Width - AnchoIndicador - 2F, 0F, AnchoIndicador - 8F, Height));
    }

    private static int Medir(string? texto, Font fuente) =>
        string.IsNullOrEmpty(texto)
            ? 0
            : TextRenderer.MeasureText(
                texto,
                fuente,
                new Size(int.MaxValue, 100),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _sobreElBoton = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _sobreElBoton = false;
        _presionado = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _presionado = true;
        Focus();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_presionado)
        {
            _presionado = false;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode is not (Keys.Space or Keys.Enter))
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        PerformClick();
    }

    private void DibujarTextos(Graphics graficos)
    {
        const TextFormatFlags banderas =
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter;

        var izquierda = MargenIzquierdo + DiametroAvatar + 10;
        var ancho = Math.Max(10, Width - izquierda - AnchoIndicador);

        if (string.IsNullOrEmpty(_textoSecundario))
        {
            TextRenderer.DrawText(
                graficos,
                Text,
                Font,
                new Rectangle(izquierda, 0, ancho, Height),
                ForeColor,
                banderas);

            return;
        }

        var altoNombre = TextRenderer.MeasureText(
            graficos,
            Text,
            Font,
            new Size(int.MaxValue, Height),
            banderas).Height;

        var altoLogin = TextRenderer.MeasureText(
            graficos,
            _textoSecundario,
            EstiloUI.FuenteMenor,
            new Size(int.MaxValue, Height),
            banderas).Height;

        var arriba = Math.Max(0, (Height - altoNombre - altoLogin) / 2);

        TextRenderer.DrawText(
            graficos,
            Text,
            Font,
            new Rectangle(izquierda, arriba, ancho, altoNombre),
            ForeColor,
            banderas);

        TextRenderer.DrawText(
            graficos,
            _textoSecundario,
            EstiloUI.FuenteMenor,
            new Rectangle(izquierda, arriba + altoNombre, ancho, altoLogin),
            EstiloUI.ConAlfa(Color.White, 185),
            banderas);
    }

    private static void DibujarIndicador(Graphics graficos, RectangleF area)
    {
        using var lapiz = new Pen(EstiloUI.ConAlfa(Color.White, 205), 1.5F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        var centroY = area.Top + (area.Height / 2F);
        graficos.DrawLines(
            lapiz,
            [
                new PointF(area.Left, centroY - 2F),
                new PointF(area.Left + (area.Width / 2F), centroY + 2.5F),
                new PointF(area.Right, centroY - 2F)
            ]);
    }
}
