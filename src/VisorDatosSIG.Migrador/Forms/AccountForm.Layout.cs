using System.Drawing;
using System.Windows.Forms;
using VisorDatosSIG.Migrador.Controls;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Construcción de la interfaz de «Mi cuenta» con la misma tarjeta premium del inicio de sesión.
/// </summary>
public sealed partial class AccountForm
{
    private readonly Label _lblTitulo = new();
    private readonly Label _lblSubtitulo = new();
    private readonly AvatarInicial _avatar = new();
    private readonly Label _lblNombre = new();
    private readonly Label _lblLogin = new();
    private readonly Panel _separador = new();
    private readonly TableLayoutPanel _tablaDatos = new();
    private readonly Label _lblValorNombre = new();
    private readonly Label _lblValorLogin = new();
    private readonly Label _lblValorRoles = new();
    private readonly Label _lblAviso = new();
    private readonly GradientButton _btnCerrar = new();

    private void InicializarInterfaz()
    {
        SuspendLayout();

        Text = "Mi cuenta · VisorDatosSIG";
        Font = EstiloUI.FuenteCuerpo;
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = EstiloUI.Fondo;
        ClientSize = new Size(440, 545);

        Controls.Add(CrearTarjeta());
        AcceptButton = _btnCerrar;

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
            RowCount = 10,
            Margin = new Padding(0)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        for (var fila = 0; fila < 10; fila++)
        {
            layout.RowStyles.Add(fila switch
            {
                5 => new RowStyle(SizeType.Absolute, 1F),
                7 => new RowStyle(SizeType.Absolute, 46F),
                8 => new RowStyle(SizeType.Percent, 100F),
                _ => new RowStyle(SizeType.AutoSize)
            });
        }

        ConfigurarEncabezado();
        ConfigurarDatos();

        layout.Controls.Add(_lblTitulo, 0, 0);
        layout.Controls.Add(_lblSubtitulo, 0, 1);
        layout.Controls.Add(_avatar, 0, 2);
        layout.Controls.Add(_lblNombre, 0, 3);
        layout.Controls.Add(_lblLogin, 0, 4);
        layout.Controls.Add(_separador, 0, 5);
        layout.Controls.Add(_tablaDatos, 0, 6);
        layout.Controls.Add(_lblAviso, 0, 7);
        layout.Controls.Add(_btnCerrar, 0, 9);

        return layout;
    }

    private void ConfigurarEncabezado()
    {
        _lblTitulo.Text = "Mi cuenta";
        _lblTitulo.Font = EstiloUI.FuenteBienvenida;
        _lblTitulo.ForeColor = EstiloUI.Violeta;
        _lblTitulo.AutoSize = true;
        _lblTitulo.Anchor = AnchorStyles.Left;
        _lblTitulo.Margin = new Padding(0, 0, 0, 2);

        _lblSubtitulo.Text = "Datos de la sesión activa";
        _lblSubtitulo.Font = EstiloUI.FuenteMenor;
        _lblSubtitulo.ForeColor = EstiloUI.TextoSecundario;
        _lblSubtitulo.AutoSize = true;
        _lblSubtitulo.Anchor = AnchorStyles.Left;
        _lblSubtitulo.Margin = new Padding(0);

        _avatar.Size = new Size(64, 64);
        _avatar.Anchor = AnchorStyles.None;
        _avatar.Margin = new Padding(0, 18, 0, 12);
        _avatar.AccessibleName = "Avatar del usuario";

        _lblNombre.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _lblNombre.ForeColor = EstiloUI.TextoPrincipal;
        _lblNombre.AutoSize = true;
        _lblNombre.Anchor = AnchorStyles.None;
        _lblNombre.Margin = new Padding(0, 0, 0, 2);

        _lblLogin.Font = EstiloUI.FuenteMenor;
        _lblLogin.ForeColor = EstiloUI.TextoSecundario;
        _lblLogin.AutoSize = true;
        _lblLogin.Anchor = AnchorStyles.None;
        _lblLogin.Margin = new Padding(0, 0, 0, 18);

        _separador.BackColor = Color.FromArgb(236, 238, 243);
        _separador.Dock = DockStyle.Fill;
        _separador.Margin = new Padding(0);
    }

    private void ConfigurarDatos()
    {
        _tablaDatos.Dock = DockStyle.Fill;
        _tablaDatos.AutoSize = true;
        _tablaDatos.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _tablaDatos.BackColor = Color.Transparent;
        _tablaDatos.ColumnCount = 2;
        _tablaDatos.RowCount = 3;
        _tablaDatos.Margin = new Padding(0, 16, 0, 0);

        _tablaDatos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
        _tablaDatos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        for (var fila = 0; fila < 3; fila++)
        {
            _tablaDatos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        AgregarDato("Nombre", _lblValorNombre, 0);
        AgregarDato("Usuario", _lblValorLogin, 1);
        AgregarDato("Rol(es)", _lblValorRoles, 2);

        _lblAviso.Font = EstiloUI.FuenteMenor;
        _lblAviso.ForeColor = EstiloUI.TextoTenue;
        _lblAviso.AutoSize = false;
        _lblAviso.Dock = DockStyle.Fill;
        _lblAviso.TextAlign = ContentAlignment.MiddleCenter;
        _lblAviso.Margin = new Padding(0, 10, 0, 0);
        _lblAviso.AccessibleName = "Estado de la verificación de la sesión";

        _btnCerrar.Text = "Cerrar";
        _btnCerrar.MostrarFlecha = false;
        _btnCerrar.Height = 44;
        _btnCerrar.Radio = 12;
        _btnCerrar.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _btnCerrar.Margin = new Padding(0, 12, 0, 0);
        _btnCerrar.AccessibleName = "Cerrar la ventana Mi cuenta";
        _btnCerrar.Click += btnCerrar_Click;
    }

    private void AgregarDato(string titulo, Label valor, int fila)
    {
        var etiqueta = new Label
        {
            Text = titulo,
            Font = EstiloUI.FuenteMenor,
            ForeColor = EstiloUI.TextoSecundario,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 10, 7)
        };

        valor.Font = EstiloUI.FuenteCuerpoDestacado;
        valor.ForeColor = EstiloUI.TextoPrincipal;
        valor.AutoSize = true;
        valor.Anchor = AnchorStyles.Left;
        valor.Margin = new Padding(0, 7, 0, 7);

        // Solo datos útiles para el usuario: nunca identificadores técnicos ni credenciales.
        _tablaDatos.Controls.Add(etiqueta, 0, fila);
        _tablaDatos.Controls.Add(valor, 1, fila);
    }
}
