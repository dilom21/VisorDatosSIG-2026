using System.Windows.Forms;
using VisorDatosSIG.Migrador.Services;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Ventana «Mi cuenta»: datos públicos de la sesión activa en modo solo lectura.
/// </summary>
/// <remarks>
/// Muestra únicamente nombre, usuario y roles. Nunca expone el token, hashes, salts,
/// identificadores internos, la cadena de conexión ni la clave JWT.
/// Al abrirse verifica la sesión contra <c>GET /api/autenticacion/me</c>; si la API no está
/// disponible se muestran los datos locales sin interrumpir al usuario.
/// </remarks>
public sealed partial class AccountForm : Form
{
    private readonly AuthenticationApiClient _cliente;
    private readonly UserSession _sesion;

    private CancellationTokenSource? _cancelacion;

    /// <summary>
    /// Inicializa el formulario con la sesión en memoria y el cliente de la API.
    /// </summary>
    /// <param name="cliente">Cliente HTTP de autenticación.</param>
    /// <param name="sesion">Sesión activa del usuario autenticado.</param>
    public AccountForm(AuthenticationApiClient cliente, UserSession sesion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));

        InicializarInterfaz();
        MostrarDatosDeSesion();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancelacion?.Cancel();
            _cancelacion?.Dispose();
            _cancelacion = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = VerificarSesionAsync();
    }

    private void btnCerrar_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }
}
