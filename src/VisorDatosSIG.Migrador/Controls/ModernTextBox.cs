using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Campo de texto con apariencia moderna: esquinas redondeadas, icono a la izquierda, foco
/// violeta y botón para mostrar u ocultar la contraseña.
/// </summary>
/// <remarks>
/// El control contiene un <see cref="TextBox"/> real (sin borde) para conservar el
/// comportamiento nativo de edición, selección y accesibilidad. La apariencia se dibuja con GDI+.
/// </remarks>
internal sealed class ModernTextBox : Control
{
    private readonly TextBox _entrada = new();
    private readonly AlternarVisibilidadButton _botonVisibilidad = new() { Visible = false };

    private bool _enfocado;
    private bool _conError;
    private bool _esContrasena;
    private ToolTip? _toolTip;

    /// <summary>Crea el campo con el estilo de los formularios de sesión.</summary>
    public ModernTextBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        // El foco lo recibe la caja de texto interna, no el contenedor.
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;

        BackColor = Color.Transparent;
        Size = new Size(320, 46);

        _entrada.BorderStyle = BorderStyle.None;
        _entrada.BackColor = EstiloUI.CampoFondo;
        _entrada.ForeColor = EstiloUI.TextoPrincipal;
        _entrada.Font = EstiloUI.FuenteCuerpo;
        _entrada.TabStop = true;
        _entrada.TextChanged += (_, _) => TextoCambiado?.Invoke(this, EventArgs.Empty);
        _entrada.GotFocus += (_, _) =>
        {
            _enfocado = true;
            Invalidate();
        };
        _entrada.LostFocus += (_, _) =>
        {
            _enfocado = false;
            Invalidate();
        };
        _entrada.KeyDown += Entrada_KeyDown;

        _botonVisibilidad.Click += (_, _) => AlternarVisibilidad();

        Controls.Add(_entrada);
        Controls.Add(_botonVisibilidad);
    }

    /// <summary>Icono que se dibuja a la izquierda del campo.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GlifoIcono? Icono { get; set; }

    /// <summary>Texto de ayuda que se muestra cuando el campo está vacío.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TextoAyuda
    {
        get => _entrada.PlaceholderText;
        set => _entrada.PlaceholderText = value;
    }

    /// <summary>Radio de las esquinas.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radio { get; set; } = 12;

    /// <summary>Contenido del campo.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Valor
    {
        get => _entrada.Text;
        set => _entrada.Text = value;
    }

    /// <summary>Longitud máxima aceptada.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LongitudMaxima
    {
        get => _entrada.MaxLength;
        set => _entrada.MaxLength = value;
    }

    /// <summary>Indica si el campo es una contraseña.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EsContrasena
    {
        get => _esContrasena;
        set
        {
            if (_esContrasena == value)
            {
                return;
            }

            _esContrasena = value;
            if (!value)
            {
                MostrarContrasena = false;
            }

            AplicarVisibilidad();
            ColocarControles();
            Invalidate();
        }
    }

    /// <summary>Indica si la contraseña se muestra en texto claro.</summary>
    public bool MostrarContrasena { get; private set; }

    /// <summary>Se produce cuando cambia el contenido del campo.</summary>
    public event EventHandler? TextoCambiado;

    /// <summary>Se produce al presionar Enter dentro del campo.</summary>
    public event EventHandler? AceptarPresionado;

    /// <summary>Caja de texto interna: se expone para pruebas de accesibilidad y foco.</summary>
    internal TextBox CajaDeTexto => _entrada;

    /// <summary>Coloca el foco en el campo.</summary>
    public void Enfocar()
    {
        if (!Visible || !Enabled)
        {
            return;
        }

        _entrada.Focus();
    }

    /// <summary>Limpia el contenido y el estado de error.</summary>
    public void Limpiar()
    {
        _entrada.Clear();
        MarcarError(false);
    }

    /// <summary>Resalta el campo como erróneo.</summary>
    public void MarcarError(bool conError)
    {
        if (_conError == conError)
        {
            return;
        }

        _conError = conError;
        Invalidate();
    }

    /// <summary>Selecciona todo el contenido del campo.</summary>
    public void SeleccionarTodo()
    {
        _entrada.Focus();
        _entrada.SelectAll();
    }

    /// <summary>Asigna el nombre que leerán los lectores de pantalla.</summary>
    public void AsignarNombreAccesible(string nombre)
    {
        AccessibleName = nombre;
        _entrada.AccessibleName = nombre;
        ActualizarAccesibilidadBoton();
    }

    /// <summary>Asocia un <see cref="ToolTip"/> para el botón de mostrar u ocultar la contraseña.</summary>
    public void AsignarToolTip(ToolTip toolTip)
    {
        _toolTip = toolTip;
        ActualizarToolTip();
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        ColocarControles();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graficos = e.Graphics;
        graficos.SmoothingMode = SmoothingMode.AntiAlias;

        var area = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
        var colorBorde = _conError
            ? EstiloUI.Error
            : _enfocado ? EstiloUI.CampoBordeFoco : EstiloUI.CampoBorde;
        var grosor = _enfocado || _conError ? 1.6F : 1.1F;

        using var ruta = EstiloUI.RutaRedondeada(area, Radio);

        using (var relleno = new SolidBrush(ColorDeFondo()))
        {
            graficos.FillPath(relleno, ruta);
        }

        using var lapiz = new Pen(colorBorde, grosor);
        graficos.DrawPath(lapiz, ruta);

        if (Icono is { } icono)
        {
            var colorIcono = _conError
                ? EstiloUI.Error
                : _enfocado ? EstiloUI.Violeta : EstiloUI.TextoTenue;

            const float lado = 17F;
            var areaIcono = new RectangleF(15F, (Height - lado) / 2F, lado, lado);
            GlifoDibujo.Dibujar(graficos, icono, areaIcono, colorIcono, 1.5F);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
        {
            Enfocar();
        }
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    private Color ColorDeFondo()
    {
        if (!Enabled)
        {
            return Color.FromArgb(247, 248, 251);
        }

        return _enfocado ? Color.FromArgb(253, 251, 255) : EstiloUI.CampoFondo;
    }

    private void ColocarControles()
    {
        var izquierda = Icono is null ? 14 : 42;
        var derecha = _esContrasena ? 46 : 14;
        var anchoEntrada = Math.Max(20, Width - izquierda - derecha);
        var altoEntrada = Math.Max(14, _entrada.PreferredHeight);
        var arriba = Math.Max(0, (Height - altoEntrada) / 2);

        _entrada.SetBounds(izquierda, arriba, anchoEntrada, altoEntrada);

        var ladoBoton = Math.Min(30, Math.Max(22, Height - 12));
        _botonVisibilidad.SetBounds(
            Width - derecha + 8,
            Math.Max(0, (Height - ladoBoton) / 2),
            ladoBoton,
            ladoBoton);
    }

    private void AlternarVisibilidad()
    {
        var posicion = _entrada.SelectionStart;

        MostrarContrasena = !MostrarContrasena;
        AplicarVisibilidad();
        ActualizarToolTip();

        _entrada.Focus();
        _entrada.SelectionStart = Math.Min(posicion, _entrada.TextLength);
        _entrada.SelectionLength = 0;
    }

    private void AplicarVisibilidad()
    {
        _botonVisibilidad.Visible = _esContrasena;
        _botonVisibilidad.ActualizarEstado(MostrarContrasena);

        // Por defecto la contraseña permanece oculta (PasswordChar del sistema).
        _entrada.PasswordChar = '\0';
        _entrada.UseSystemPasswordChar = _esContrasena && !MostrarContrasena;

        ActualizarAccesibilidadBoton();
    }

    private void ActualizarAccesibilidadBoton() =>
        _botonVisibilidad.AccessibleName = MostrarContrasena
            ? "Ocultar contraseña"
            : "Mostrar contraseña";

    private void ActualizarToolTip() =>
        _toolTip?.SetToolTip(
            _botonVisibilidad,
            MostrarContrasena ? "Ocultar contraseña" : "Mostrar contraseña");

    private void Entrada_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        AceptarPresionado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Botón interno que alterna la visibilidad de la contraseña: dibuja un ojo abierto o tachado
    /// según el estado.
    /// </summary>
    private sealed class AlternarVisibilidadButton : Control
    {
        private bool _contrasenaVisible;
        private bool _sobreElBoton;

        public AlternarVisibilidadButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor,
                true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
            AccessibleRole = AccessibleRole.PushButton;
        }

        /// <summary>Actualiza el icono según la visibilidad de la contraseña.</summary>
        public void ActualizarEstado(bool contrasenaVisible)
        {
            _contrasenaVisible = contrasenaVisible;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var color = _sobreElBoton || _contrasenaVisible ? EstiloUI.Violeta : EstiloUI.TextoTenue;
            var glifo = _contrasenaVisible ? GlifoIcono.OjoTachado : GlifoIcono.Ojo;

            var area = new RectangleF(4F, 0F, Math.Max(1F, Width - 8F), Math.Max(1F, Height));
            GlifoDibujo.Dibujar(e.Graphics, glifo, area, color, 1.4F);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _sobreElBoton = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _sobreElBoton = false;
            Invalidate();
        }
    }
}
