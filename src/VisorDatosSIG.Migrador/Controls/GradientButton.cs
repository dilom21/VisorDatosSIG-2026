using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Botón principal del Migrador: degradado violeta → azul, esquinas redondeadas, estados de
/// hover/pressed, indicador de carga y foco visible.
/// </summary>
/// <remarks>
/// Se dibuja con GDI+ para lograr una apariencia moderna sin librerías externas. Implementa
/// <see cref="IButtonControl"/> para poder usarse como botón predeterminado (<c>AcceptButton</c>)
/// del formulario de inicio de sesión.
/// </remarks>
internal sealed class GradientButton : Control, IButtonControl
{
    private readonly System.Windows.Forms.Timer _animacion = new() { Interval = 45 };

    private bool _sobreElBoton;
    private bool _presionado;
    private bool _enfocado;
    private bool _esPredeterminado;
    private bool _enProgreso;
    private float _angulo;

    /// <summary>Crea el botón con el estilo principal de VisorDatosSIG.</summary>
    public GradientButton()
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
        Font = EstiloUI.FuenteBoton;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(220, 48);
        AccessibleRole = AccessibleRole.PushButton;

        _animacion.Tick += (_, _) =>
        {
            _angulo = (_angulo + 26F) % 360F;
            Invalidate();
        };
    }

    /// <summary>Color inicial del degradado (violeta).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ColorInicio { get; set; } = EstiloUI.GradienteInicio;

    /// <summary>Color final del degradado (azul).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ColorFin { get; set; } = EstiloUI.GradienteFin;

    /// <summary>Radio de las esquinas.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radio { get; set; } = 14;

    /// <summary>Indica si se dibuja la flecha al final del texto.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MostrarFlecha { get; set; } = true;

    /// <summary>Texto que se muestra mientras la operación está en curso.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TextoEnProgreso { get; set; } = "Procesando...";

    /// <summary>Indica que hay una operación en curso: bloquea clics y muestra el indicador de carga.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EnProgreso
    {
        get => _enProgreso;
        set
        {
            if (_enProgreso == value)
            {
                return;
            }

            _enProgreso = value;
            _presionado = false;

            if (value)
            {
                _animacion.Start();
            }
            else
            {
                _animacion.Stop();
                _angulo = 0F;
            }

            Cursor = Enabled && !value ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    /// <summary>Resultado del diálogo cuando el botón se usa como botón de comando.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DialogResult DialogResult { get; set; } = DialogResult.None;

    /// <inheritdoc />
    public void NotifyDefault(bool value)
    {
        if (_esPredeterminado == value)
        {
            return;
        }

        _esPredeterminado = value;
        Invalidate();
    }

    /// <inheritdoc />
    public void PerformClick()
    {
        if (!Enabled || EnProgreso)
        {
            return;
        }

        OnClick(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(220, 48);

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graficos = e.Graphics;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        var rectangulo = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
        if (_presionado && !EnProgreso)
        {
            rectangulo = new RectangleF(0.5F, 1.5F, Width - 1F, Height - 2F);
        }

        using var ruta = EstiloUI.RutaRedondeada(rectangulo, Radio);

        if (!Enabled)
        {
            using var gris = new SolidBrush(Color.FromArgb(206, 212, 222));
            graficos.FillPath(gris, ruta);
            using var bordeGris = new Pen(Color.FromArgb(190, 197, 208), 1F);
            graficos.DrawPath(bordeGris, ruta);
        }
        else
        {
            var inicio = ColorInicio;
            var fin = ColorFin;

            if (_presionado)
            {
                inicio = EstiloUI.Mezclar(inicio, Color.Black, 0.12);
                fin = EstiloUI.Mezclar(fin, Color.Black, 0.12);
            }
            else if (_sobreElBoton)
            {
                inicio = EstiloUI.Mezclar(inicio, Color.White, 0.20);
                fin = EstiloUI.Mezclar(fin, Color.White, 0.20);
            }

            using var pincel = new LinearGradientBrush(rectangulo, inicio, fin, LinearGradientMode.Horizontal);
            graficos.FillPath(pincel, ruta);
        }

        DibujarFoco(graficos, rectangulo);
        DibujarContenido(graficos, rectangulo);
    }

    private void DibujarFoco(Graphics graficos, RectangleF rectangulo)
    {
        if (!_enfocado)
        {
            return;
        }

        var interior = RectangleF.Inflate(rectangulo, -4F, -4F);
        using var rutaFoco = EstiloUI.RutaRedondeada(interior, Math.Max(2, Radio - 4));
        using var lapizFoco = new Pen(EstiloUI.ConAlfa(Color.White, 185), 1.4F)
        {
            DashStyle = DashStyle.Dot
        };

        graficos.DrawPath(lapizFoco, rutaFoco);
    }

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

        if (e.Button != MouseButtons.Left || EnProgreso)
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
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _enfocado = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _enfocado = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled && !EnProgreso ? Cursors.Hand : Cursors.Default;
        Invalidate();
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

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        // Evita la doble petición mientras la operación está en curso.
        if (EnProgreso)
        {
            return;
        }

        base.OnClick(e);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animacion.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DibujarContenido(Graphics graficos, RectangleF rectangulo)
    {
        var texto = EnProgreso ? TextoEnProgreso : Text;
        if (string.IsNullOrEmpty(texto))
        {
            return;
        }

        var colorTexto = Enabled ? ForeColor : Color.FromArgb(250, 250, 252);

        const TextFormatFlags banderas =
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter;

        var tamanoTexto = TextRenderer.MeasureText(
            graficos,
            texto,
            Font,
            new Size(int.MaxValue, (int)Math.Ceiling(rectangulo.Height)),
            banderas);

        var mostrarAdorno = EnProgreso || MostrarFlecha;
        var anchoAdorno = EnProgreso ? 20F : 18F;
        var separacion = mostrarAdorno ? 10F : 0F;
        var anchoTotal = tamanoTexto.Width + separacion + (mostrarAdorno ? anchoAdorno : 0F);
        var inicioTexto = rectangulo.Left + ((rectangulo.Width - anchoTotal) / 2F);

        TextRenderer.DrawText(
            graficos,
            texto,
            Font,
            new Rectangle(
                (int)Math.Round(inicioTexto),
                (int)rectangulo.Top,
                tamanoTexto.Width + 2,
                (int)Math.Round(rectangulo.Height)),
            colorTexto,
            banderas);

        if (!mostrarAdorno)
        {
            return;
        }

        var areaAdorno = new RectangleF(
            inicioTexto + tamanoTexto.Width + separacion,
            rectangulo.Top + ((rectangulo.Height - 16F) / 2F),
            anchoAdorno,
            16F);

        if (!EnProgreso)
        {
            GlifoDibujo.DibujarFlecha(graficos, areaAdorno, colorTexto, 1.8F);
            return;
        }

        using var lapiz = new Pen(EstiloUI.ConAlfa(Color.White, 225), 2.2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graficos.DrawArc(lapiz, RectangleF.Inflate(areaAdorno, 3F, 3F), _angulo, 110F);
    }
}
