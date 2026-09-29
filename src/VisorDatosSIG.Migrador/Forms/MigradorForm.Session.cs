using System.Windows.Forms;
using VisorDatosSIG.Migrador.Controls;
using VisorDatosSIG.Migrador.Services;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Sesión del usuario en el Migrador: cabecera, menú del usuario, «Mi cuenta» y cierre de sesión.
/// </summary>
/// <remarks>
/// El Migrador nunca accede a SQL Server: todo pasa por <see cref="AuthenticationApiClient"/>.
/// El token JWT vive únicamente en memoria (UserSession) y no se registra en bitácora, archivos
/// ni configuración. Las fases 1 y 2 (inspección y validación) funcionan sin sesión.
/// </remarks>
public sealed partial class MigradorForm
{
    private MenuUsuario? _menuUsuario;

    private void InicializarSesion()
    {
        _menuUsuario = new MenuUsuario();
        _menuUsuario.CuentaSeleccionada += (_, _) => AbrirMiCuenta();
        _menuUsuario.CerrarSesionSeleccionado += (_, _) => _ = CerrarSesionAsync();

        _sesion.EstadoCambiado += Sesion_EstadoCambiado;
        _apiClient.SesionExpirada += Api_SesionExpirada;

        ActualizarCabeceraSesion();
    }

    private void LiberarSesion()
    {
        _sesion.EstadoCambiado -= Sesion_EstadoCambiado;
        _apiClient.SesionExpirada -= Api_SesionExpirada;

        _menuUsuario?.Dispose();
        _menuUsuario = null;
    }

    /// <summary>
    /// Refleja el estado de la sesión en la cabecera: «Iniciar sesión» o el usuario identificado.
    /// </summary>
    private void ActualizarCabeceraSesion()
    {
        var autenticado = _sesion.IsAuthenticated;

        if (autenticado)
        {
            _chipUsuario.Text = _sesion.NombreParaMostrar;
            _chipUsuario.Iniciales = _sesion.Iniciales;
            _chipUsuario.TextoSecundario = _sesion.Login;
            _toolTip.SetToolTip(
                _chipUsuario,
                $"{_sesion.NombreParaMostrar} ({_sesion.Login}) · Rol: {_sesion.RolesTexto}");

            _menuUsuario?.EstablecerUsuario(_sesion.NombreParaMostrar, _sesion.Login);

            _chipUsuario.Visible = true;
            _btnIniciarSesion.Visible = false;
            _areaSesion.Size = new Size(Math.Max(_chipUsuario.Width, _btnIniciarSesion.Width), 44);
        }
        else
        {
            _chipUsuario.Visible = false;
            _btnIniciarSesion.Visible = true;
            _areaSesion.Size = new Size(_btnIniciarSesion.Width, 44);
        }

        // La zona tiene tamaño explícito: se vuelve a diseñar para repintar el control visible.
        _areaSesion.PerformLayout();
        _chipUsuario.Invalidate();
        _btnIniciarSesion.Invalidate();
    }

    private void btnIniciarSesion_Click(object? sender, EventArgs e)
    {
        using var formulario = new LoginForm(_apiClient, _sesion);
        var resultado = formulario.ShowDialog(this);

        if (resultado == DialogResult.OK && _sesion.IsAuthenticated)
        {
            EstablecerActividad(
                $"Sesión iniciada: {_sesion.NombreParaMostrar} ({_sesion.Login}) · Rol: {_sesion.RolesTexto}.");
        }
    }

    private void btnUsuario_Click(object? sender, EventArgs e)
    {
        if (!_sesion.IsAuthenticated)
        {
            return;
        }

        _menuUsuario?.EstablecerUsuario(_sesion.NombreParaMostrar, _sesion.Login);
        _menuUsuario?.Mostrar(_chipUsuario);
    }

    private void AbrirMiCuenta()
    {
        if (!_sesion.IsAuthenticated)
        {
            return;
        }

        using var formulario = new AccountForm(_apiClient, _sesion);
        formulario.ShowDialog(this);
    }

    /// <summary>
    /// Cierra la sesión: registra el cierre en la API y limpia la sesión local.
    /// </summary>
    /// <remarks>
    /// Aunque falle la comunicación con el servidor, la sesión local siempre se elimina: el
    /// cierre de sesión del Migrador no depende de la red.
    /// </remarks>
    private async Task CerrarSesionAsync()
    {
        if (!_sesion.IsAuthenticated)
        {
            return;
        }

        _menuUsuario?.Ocultar();

        var token = _sesion.AccessToken;
        EstablecerActividad("Cerrando la sesión...");

        var resultado = await _apiClient.CerrarSesionAsync(token);

        var mensaje = resultado.EsExitoso || resultado.Estado == EstadoOperacion.SesionExpirada
            ? "Sesión cerrada."
            : $"Sesión cerrada localmente. {resultado.Mensaje}";

        // Primero se informa el resultado y después se limpia la sesión
        // (limpiarla actualiza la cabecera y el mensaje nunca debe quedar pendiente).
        if (FormularioActivo)
        {
            EstablecerActividad(mensaje);
        }

        _sesion.Clear();
    }

    private void Sesion_EstadoCambiado(object? sender, EventArgs e)
    {
        if (FormularioActivo)
        {
            ActualizarCabeceraSesion();
        }
    }

    /// <summary>
    /// Atiende el vencimiento del token detectado por el cliente de la API: limpia la sesión,
    /// devuelve la cabecera al estado no autenticado y avisa al usuario.
    /// </summary>
    private void Api_SesionExpirada(object? sender, EventArgs e)
    {
        if (!FormularioActivo)
        {
            return;
        }

        _menuUsuario?.Ocultar();

        if (!_sesion.IsAuthenticated)
        {
            return;
        }

        _sesion.Clear();
        EstablecerActividad(MensajesAutenticacion.SesionExpirada);

        MessageBox.Show(
            this,
            MensajesAutenticacion.SesionExpirada,
            "Sesión finalizada",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
