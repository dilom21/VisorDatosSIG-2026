using System.Drawing;
using System.Windows.Forms;
using VisorDatosSIG.Migrador.Controls;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Construcción de la interfaz del inicio de sesión: tarjeta central con la identidad visual
/// de VisorDatosSIG (violeta → azul).
/// </summary>
/// <remarks>
/// El diseño en código usa TableLayoutPanel, Dock y Anchor para que la tarjeta se adapte a los
/// escalados del 100 %, 125 % y 150 %. No se incluyen «Recordarme», «Olvidé mi contraseña» ni
/// «Registrarse» porque están fuera del alcance del proyecto.
/// </remarks>
public sealed partial class LoginForm
{
    private readonly Label _lblMarca = new();
    private readonly Label _lblBienvenida = new();
    private readonly Label _lblInstruccion = new();
    private readonly BarraAcento _barraAcento = new();
    private readonly Label _lblUsuario = new();
    private readonly ModernTextBox _txtUsuario = new();
    private readonly Label _lblContrasena = new();
    private readonly ModernTextBox _txtContrasena = new();
    private readonly Label _lblMensaje = new();
    private readonly GradientButton _btnIngresar = new();
    private readonly Label _lblPie = new();

    private void InicializarInterfaz()
    {
        SuspendLayout();

        Text = "Iniciar sesión · VisorDatosSIG";
        Font = EstiloUI.FuenteCuerpo;
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = EstiloUI.Fondo;
        ClientSize = new Size(440, 520);
        KeyPreview = true;

        Controls.Add(CrearTarjeta());
        AcceptButton = _btnIngresar;

        ResumeLayout(true);
    }

    private Control CrearTarjeta()
    {
        var contenedor = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = EstiloUI.Fondo,
            Padding = new Padding(20, 20, 16, 16)
        };

        var tarjeta = new CardPanel
        {
            Dock = DockStyle.Fill,
            Radio = 20
        };

        tarjeta.Controls.Add(CrearContenidoTarjeta());
        contenedor.Controls.Add(tarjeta);
        return contenedor;
    }

    private Control CrearContenidoTarjeta()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 13,
            Margin = new Padding(0)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        for (var fila = 0; fila < 13; fila++)
        {
            layout.RowStyles.Add(fila is 4 or 11
                ? new RowStyle(SizeType.Percent, 100F)
                : fila == 9
                    ? new RowStyle(SizeType.Absolute, 46F)
                    : new RowStyle(SizeType.AutoSize));
        }

        ConfigurarTitulos();

        layout.Controls.Add(_lblMarca, 0, 0);
        layout.Controls.Add(_lblBienvenida, 0, 1);
        layout.Controls.Add(_lblInstruccion, 0, 2);
        layout.Controls.Add(_barraAcento, 0, 3);
        layout.Controls.Add(_lblUsuario, 0, 5);
        layout.Controls.Add(_txtUsuario, 0, 6);
        layout.Controls.Add(_lblContrasena, 0, 7);
        layout.Controls.Add(_txtContrasena, 0, 8);
        layout.Controls.Add(_lblMensaje, 0, 9);
        layout.Controls.Add(_btnIngresar, 0, 10);
        layout.Controls.Add(_lblPie, 0, 12);

        return layout;
    }

    private void ConfigurarTitulos()
    {
        _lblMarca.Text = "VisorDatosSIG";
        _lblMarca.Font = EstiloUI.FuenteTitulo;
        _lblMarca.ForeColor = EstiloUI.Violeta;
        CentrarEtiqueta(_lblMarca, new Padding(0, 2, 0, 0));

        _lblBienvenida.Text = "Bienvenido";
        _lblBienvenida.Font = EstiloUI.FuenteBienvenida;
        _lblBienvenida.ForeColor = EstiloUI.TextoPrincipal;
        CentrarEtiqueta(_lblBienvenida, new Padding(0, 12, 0, 0));

        _lblInstruccion.Text = "Inicia sesión para continuar";
        _lblInstruccion.Font = EstiloUI.FuenteCuerpo;
        _lblInstruccion.ForeColor = EstiloUI.TextoSecundario;
        CentrarEtiqueta(_lblInstruccion, new Padding(0, 2, 0, 0));

        _barraAcento.Anchor = AnchorStyles.None;
        _barraAcento.Margin = new Padding(0, 12, 0, 0);

        _lblPie.Text = "VisorDatosSIG 2026 · Proyecto Integrador SIG";
        _lblPie.Font = EstiloUI.FuenteMenor;
        _lblPie.ForeColor = EstiloUI.TextoTenue;
        CentrarEtiqueta(_lblPie, new Padding(0, 10, 0, 0));

        ConfigurarCampos();
        ConfigurarBoton();
        ConfigurarMensaje();
    }

    private void ConfigurarCampos()
    {
        _lblUsuario.Text = "Usuario";
        _lblUsuario.Font = EstiloUI.FuenteEtiqueta;
        _lblUsuario.ForeColor = EstiloUI.TextoPrincipal;
        _lblUsuario.AutoSize = true;
        _lblUsuario.Anchor = AnchorStyles.Left;
        _lblUsuario.Margin = new Padding(2, 4, 0, 6);

        _txtUsuario.Icono = GlifoIcono.Usuario;
        _txtUsuario.TextoAyuda = "Ingrese su usuario";
        _txtUsuario.Height = 46;
        _txtUsuario.LongitudMaxima = 100;
        _txtUsuario.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _txtUsuario.Margin = new Padding(0);
        _txtUsuario.AsignarNombreAccesible("Usuario");
        _txtUsuario.AsignarToolTip(_toolTip);
        _txtUsuario.AceptarPresionado += Campo_AceptarPresionado;
        _txtUsuario.TextoCambiado += Campo_TextoCambiado;

        _lblContrasena.Text = "Contraseña";
        _lblContrasena.Font = EstiloUI.FuenteEtiqueta;
        _lblContrasena.ForeColor = EstiloUI.TextoPrincipal;
        _lblContrasena.AutoSize = true;
        _lblContrasena.Anchor = AnchorStyles.Left;
        _lblContrasena.Margin = new Padding(2, 16, 0, 6);

        _txtContrasena.Icono = GlifoIcono.Candado;
        _txtContrasena.TextoAyuda = "Ingrese su contraseña";
        _txtContrasena.Height = 46;
        _txtContrasena.EsContrasena = true;
        _txtContrasena.LongitudMaxima = 200;
        _txtContrasena.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _txtContrasena.Margin = new Padding(0);
        _txtContrasena.AsignarNombreAccesible("Contraseña");
        _txtContrasena.AsignarToolTip(_toolTip);
        _txtContrasena.AceptarPresionado += Campo_AceptarPresionado;
        _txtContrasena.TextoCambiado += Campo_TextoCambiado;
    }

    private void ConfigurarBoton()
    {
        _btnIngresar.Text = "INICIAR SESIÓN";
        _btnIngresar.MostrarFlecha = true;
        _btnIngresar.TextoEnProgreso = "Iniciando sesión...";
        _btnIngresar.Height = 48;
        _btnIngresar.Radio = 14;
        _btnIngresar.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _btnIngresar.Margin = new Padding(0, 18, 0, 0);
        _btnIngresar.AccessibleName = "Iniciar sesión";
        _btnIngresar.Click += btnIngresar_Click;

        _toolTip.SetToolTip(
            _btnIngresar,
            $"Servidor de autenticación: {_cliente.DireccionBase?.AbsoluteUri.TrimEnd('/')}");
    }

    private void ConfigurarMensaje()
    {
        _lblMensaje.Text = string.Empty;
        _lblMensaje.Font = EstiloUI.FuenteCuerpo;
        _lblMensaje.ForeColor = EstiloUI.TextoSecundario;
        _lblMensaje.AutoSize = false;
        _lblMensaje.Dock = DockStyle.Fill;
        _lblMensaje.TextAlign = ContentAlignment.MiddleCenter;
        _lblMensaje.Margin = new Padding(0);
        _lblMensaje.AccessibleName = "Estado del inicio de sesión";
    }

    private static void CentrarEtiqueta(Label etiqueta, Padding margen)
    {
        etiqueta.AutoSize = true;
        etiqueta.Anchor = AnchorStyles.None;
        etiqueta.Margin = margen;
    }
}
