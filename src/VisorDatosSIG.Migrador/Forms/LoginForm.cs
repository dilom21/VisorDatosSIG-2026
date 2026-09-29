using System.Drawing;
using System.Windows.Forms;
using VisorDatosSIG.Migrador.Controls;
using VisorDatosSIG.Migrador.Services;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Ventana de inicio de sesión del Migrador.
/// </summary>
/// <remarks>
/// <para>
/// Consume exclusivamente la API HTTP (nunca SQL Server) y deja el token en memoria dentro de
/// <see cref="UserSession"/>. El Migrador no conoce la cadena de conexión, la clave JWT,
/// contraseñas almacenadas ni datos internos del servidor.
/// </para>
/// <para>
/// El formulario no se cierra cuando el inicio de sesión falla: el mensaje se muestra dentro de
/// la tarjeta y la contraseña no se conserva tras un acceso correcto.
/// </para>
/// </remarks>
public sealed partial class LoginForm : Form
{
    private readonly AuthenticationApiClient _cliente;
    private readonly UserSession _sesion;
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _temporizadorExito = new() { Interval = 450 };

    private CancellationTokenSource? _cancelacion;
    private bool _autenticando;
    private bool _exito;

    /// <summary>
    /// Inicializa el formulario con el cliente de la API y la sesión en memoria.
    /// </summary>
    /// <param name="cliente">Cliente HTTP de autenticación.</param>
    /// <param name="sesion">Sesión en memoria que se completará al autenticarse.</param>
    public LoginForm(AuthenticationApiClient cliente, UserSession sesion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));

        InicializarInterfaz();
        _temporizadorExito.Tick += (_, _) => CerrarTrasExito();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancelacion?.Cancel();
            _cancelacion?.Dispose();
            _cancelacion = null;
            _temporizadorExito.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Indica si el inicio de sesión terminó correctamente durante la vida del formulario.</summary>
    public bool SesionIniciada => _exito;

    private void btnIngresar_Click(object? sender, EventArgs e)
    {
        if (!_exito)
        {
            _ = IniciarSesionSeguroAsync();
        }
    }

    private void Campo_AceptarPresionado(object? sender, EventArgs e)
    {
        if (!_autenticando && !_exito)
        {
            _ = IniciarSesionSeguroAsync();
        }
    }

    /// <inheritdoc />
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            CancelarOCerrar();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <inheritdoc />
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _txtUsuario.Enfocar();
    }
}
