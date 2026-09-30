using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Menú del usuario autenticado: encabezado con avatar, nombre y login, y las opciones
/// «Mi cuenta» y «Cerrar sesión».
/// </summary>
/// <remarks>
/// No incluye «Recordarme», «Olvidé mi contraseña» ni «Registrarse»: están fuera del alcance
/// del proyecto. El menú se dibuja con <see cref="MenuSesionRenderer"/> para no mostrar la
/// apariencia gris estándar de Windows Forms.
/// </remarks>
internal sealed class MenuUsuario : IDisposable
{
    private const int AnchoMenu = 248;

    private readonly ContextMenuStrip _menu = new();
    private readonly AvatarInicial _avatar = new();
    private readonly Label _lblNombre = new();
    private readonly Label _lblLogin = new();
    private readonly List<Image> _imagenes = [];

    private bool _disposed;

    /// <summary>Crea el menú con sus opciones.</summary>
    public MenuUsuario()
    {
        _menu.Renderer = new MenuSesionRenderer();
        _menu.BackColor = Color.White;
        _menu.Font = EstiloUI.FuenteCuerpo;
        _menu.Padding = new Padding(4, 4, 4, 6);
        _menu.ShowImageMargin = true;
        _menu.ShowCheckMargin = false;
        _menu.DropShadowEnabled = true;
        _menu.MinimumSize = new Size(AnchoMenu, 0);

        var itemCuenta = CrearOpcion("Mi cuenta", GlifoIcono.Usuario);
        itemCuenta.Click += (_, _) => CuentaSeleccionada?.Invoke(this, EventArgs.Empty);

        var itemCerrar = CrearOpcion("Cerrar sesión", GlifoIcono.Salir);
        itemCerrar.Click += (_, _) => CerrarSesionSeleccionado?.Invoke(this, EventArgs.Empty);

        _menu.Items.Add(CrearEncabezado());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(itemCuenta);
        _menu.Items.Add(itemCerrar);
    }

    /// <summary>Se produce al elegir «Mi cuenta».</summary>
    public event EventHandler? CuentaSeleccionada;

    /// <summary>Se produce al elegir «Cerrar sesión».</summary>
    public event EventHandler? CerrarSesionSeleccionado;

    /// <summary>Actualiza los datos del encabezado del menú.</summary>
    /// <param name="nombre">Nombre completo del usuario.</param>
    /// <param name="login">Login del usuario.</param>
    public void EstablecerUsuario(string nombre, string login)
    {
        var nombreMostrado = string.IsNullOrWhiteSpace(nombre) ? login : nombre;

        _lblNombre.Text = nombreMostrado;
        _lblLogin.Text = login;
        _avatar.Iniciales = UserSession.CalcularIniciales(nombre, login);
        _menu.AccessibleName = $"Menú del usuario {nombreMostrado}";
    }

    /// <summary>Muestra el menú alineado al borde derecho del control indicado.</summary>
    /// <param name="ancla">Control desde el cual se despliega el menú.</param>
    public void Mostrar(Control ancla)
    {
        ArgumentNullException.ThrowIfNull(ancla);

        _menu.Show(ancla, new Point(ancla.Width, ancla.Height + 6), ToolStripDropDownDirection.BelowLeft);
    }

    /// <summary>Cierra el menú si está desplegado.</summary>
    public void Ocultar()
    {
        if (_menu.Visible)
        {
            _menu.Close();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _menu.Dispose();

        foreach (var imagen in _imagenes)
        {
            imagen.Dispose();
        }

        _imagenes.Clear();
    }

    private ToolStripMenuItem CrearOpcion(string texto, GlifoIcono glifo)
    {
        var imagen = new Bitmap(16, 16);

        using (var graficos = Graphics.FromImage(imagen))
        {
            graficos.SmoothingMode = SmoothingMode.AntiAlias;
            GlifoDibujo.Dibujar(graficos, glifo, new RectangleF(1F, 1F, 14F, 14F), EstiloUI.TextoSecundario, 1.4F);
        }

        _imagenes.Add(imagen);

        return new ToolStripMenuItem(texto)
        {
            Image = imagen,
            ImageScaling = ToolStripItemImageScaling.None,
            ForeColor = EstiloUI.TextoPrincipal,
            Padding = new Padding(4, 5, 4, 5),
            AccessibleName = texto
        };
    }

    private ToolStripControlHost CrearEncabezado()
    {
        _avatar.Size = new Size(38, 38);
        _avatar.Anchor = AnchorStyles.Left;
        _avatar.Margin = new Padding(0);

        _lblNombre.AutoSize = true;
        _lblNombre.Font = EstiloUI.FuenteCuerpoDestacado;
        _lblNombre.ForeColor = EstiloUI.TextoPrincipal;
        _lblNombre.Margin = new Padding(0, 2, 0, 0);

        _lblLogin.AutoSize = true;
        _lblLogin.Font = EstiloUI.FuenteMenor;
        _lblLogin.ForeColor = EstiloUI.TextoSecundario;
        _lblLogin.Margin = new Padding(0);

        var contenido = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(6, 6, 6, 6),
            Margin = new Padding(0)
        };

        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46F));
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        contenido.Controls.Add(_avatar, 0, 0);
        contenido.SetRowSpan(_avatar, 2);
        contenido.Controls.Add(_lblNombre, 1, 0);
        contenido.Controls.Add(_lblLogin, 1, 1);

        return new ToolStripControlHost(contenido)
        {
            AutoSize = false,
            Size = new Size(AnchoMenu - 10, 58),
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.White,
            // El encabezado solo informa: no se resalta ni recibe foco.
            Enabled = false
        };
    }
}
