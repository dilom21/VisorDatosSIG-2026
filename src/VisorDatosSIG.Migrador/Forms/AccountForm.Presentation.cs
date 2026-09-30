using System.Drawing;
using System.Windows.Forms;
using VisorDatosSIG.Migrador.Controls;
using VisorDatosSIG.Migrador.Services;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Presentación de «Mi cuenta»: datos de la sesión y verificación opcional contra la API.
/// </summary>
public sealed partial class AccountForm
{
    /// <summary>Refresca las etiquetas con los datos de la sesión en memoria.</summary>
    private void MostrarDatosDeSesion()
    {
        _avatar.Iniciales = _sesion.Iniciales;
        _lblNombre.Text = _sesion.NombreParaMostrar;
        _lblLogin.Text = _sesion.Login;
        _lblValorNombre.Text = _sesion.NombreParaMostrar;
        _lblValorLogin.Text = _sesion.Login;
        _lblValorRoles.Text = _sesion.RolesTexto;
    }

    /// <summary>
    /// Verifica la sesión con <c>GET /api/autenticacion/me</c>.
    /// </summary>
    /// <remarks>
    /// Si el token expiró (401) el cliente de la API avisa a la ventana principal y este
    /// formulario se cierra. Si la API no responde, se mantienen los datos locales sin error.
    /// </remarks>
    private async Task VerificarSesionAsync()
    {
        if (!_sesion.IsAuthenticated)
        {
            EstablecerAviso("No hay una sesión activa.", EstiloUI.Error);
            return;
        }

        _cancelacion = new CancellationTokenSource();

        try
        {
            var resultado = await _cliente.ObtenerUsuarioActualAsync(_sesion.AccessToken, _cancelacion.Token);
            if (IsDisposed)
            {
                return;
            }

            switch (resultado.Estado)
            {
                case EstadoOperacion.Exito when resultado.Datos is { } usuario:
                    _sesion.ActualizarUsuario(usuario);
                    MostrarDatosDeSesion();
                    EstablecerAviso("Datos verificados con el servidor.", EstiloUI.TextoTenue);
                    break;

                case EstadoOperacion.SesionExpirada:
                    EstablecerAviso(MensajesAutenticacion.SesionExpirada, EstiloUI.Error);
                    DialogResult = DialogResult.Cancel;
                    Close();
                    break;

                case EstadoOperacion.Cancelado:
                    break;

                default:
                    EstablecerAviso(
                        "No se pudo verificar la sesión con el servidor. Se muestran los datos locales.",
                        EstiloUI.TextoTenue);
                    break;
            }
        }
        catch (Exception)
        {
            if (!IsDisposed)
            {
                EstablecerAviso(
                    "No se pudo verificar la sesión con el servidor. Se muestran los datos locales.",
                    EstiloUI.TextoTenue);
            }
        }
        finally
        {
            _cancelacion?.Dispose();
            _cancelacion = null;
        }
    }

    private void EstablecerAviso(string texto, Color color)
    {
        _lblAviso.Text = texto;
        _lblAviso.ForeColor = color;
    }
}
