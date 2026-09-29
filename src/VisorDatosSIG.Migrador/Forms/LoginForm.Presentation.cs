using System.Drawing;
using System.Windows.Forms;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Migrador.Controls;
using VisorDatosSIG.Migrador.Services;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Estados del inicio de sesión: normal, cargando, error y éxito.
/// </summary>
/// <remarks>
/// La validación local (usuario y contraseña no vacíos) se ejecuta antes de cualquier petición
/// HTTP. Los mensajes se muestran siempre dentro de la tarjeta, sin depender de cuadros de diálogo,
/// y nunca incluyen trazas ni detalles internos del servidor.
/// </remarks>
public sealed partial class LoginForm
{
    private async Task IniciarSesionSeguroAsync()
    {
        try
        {
            await IniciarSesionAsync();
        }
        catch (Exception)
        {
            if (IsDisposed)
            {
                return;
            }

            EstablecerModoCarga(false);
            MostrarMensaje(MensajesAutenticacion.ErrorServidor, EstiloUI.Error);
        }
    }

    private async Task IniciarSesionAsync()
    {
        var usuario = _txtUsuario.Valor.Trim();
        var contrasena = _txtContrasena.Valor;

        if (!ValidarDatos(usuario, contrasena))
        {
            return;
        }

        _cancelacion = new CancellationTokenSource();
        EstablecerModoCarga(true);

        try
        {
            var resultado = await _cliente.IniciarSesionAsync(usuario, contrasena, _cancelacion.Token);
            if (IsDisposed || _exito)
            {
                return;
            }

            switch (resultado.Estado)
            {
                case EstadoOperacion.Exito when resultado.Datos is { } respuesta:
                    GuardarSesion(respuesta);
                    break;

                case EstadoOperacion.Cancelado:
                    MostrarMensaje(MensajesAutenticacion.Cancelado, EstiloUI.TextoSecundario);
                    break;

                default:
                    MostrarErrorDeInicioSesion(resultado);
                    break;
            }
        }
        finally
        {
            _cancelacion?.Dispose();
            _cancelacion = null;

            if (!IsDisposed && !_exito)
            {
                EstablecerModoCarga(false);
            }
        }
    }

    private bool ValidarDatos(string usuario, string contrasena)
    {
        _txtUsuario.MarcarError(false);
        _txtContrasena.MarcarError(false);

        if (usuario.Length == 0)
        {
            _txtUsuario.MarcarError(true);
            _txtUsuario.Enfocar();
            MostrarMensaje("Ingrese su usuario.", EstiloUI.Error);
            return false;
        }

        if (contrasena.Length == 0)
        {
            _txtContrasena.MarcarError(true);
            _txtContrasena.Enfocar();
            MostrarMensaje("Ingrese su contraseña.", EstiloUI.Error);
            return false;
        }

        return true;
    }

    private void GuardarSesion(LoginResponseDto respuesta)
    {
        _sesion.Establecer(respuesta.AccessToken, respuesta.Usuario, respuesta.ExpiresIn);

        // La contraseña no se conserva en el formulario tras un acceso correcto.
        _txtContrasena.Limpiar();
        _exito = true;

        EstablecerModoCarga(false);
        EstablecerCamposHabilitados(false);

        _btnIngresar.Text = "Acceso correcto";
        _btnIngresar.MostrarFlecha = false;

        MostrarMensaje($"Bienvenido, {_sesion.NombreParaMostrar}.", EstiloUI.Exito);
        _temporizadorExito.Start();
    }

    private void CerrarTrasExito()
    {
        _temporizadorExito.Stop();

        if (IsDisposed)
        {
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void MostrarErrorDeInicioSesion(ResultadoApi<LoginResponseDto> resultado)
    {
        switch (resultado.Estado)
        {
            case EstadoOperacion.CredencialesInvalidas:
                _txtContrasena.MarcarError(true);
                _txtContrasena.SeleccionarTodo();
                break;

            case EstadoOperacion.SolicitudInvalida:
            case EstadoOperacion.CuentaInhabilitada:
            case EstadoOperacion.SinPermisos:
                _txtUsuario.MarcarError(true);
                _txtContrasena.MarcarError(true);
                break;
        }

        MostrarMensaje(resultado.Mensaje, EstiloUI.Error);
    }

    private void Campo_TextoCambiado(object? sender, EventArgs e)
    {
        if (_autenticando || _exito)
        {
            return;
        }

        _txtUsuario.MarcarError(false);
        _txtContrasena.MarcarError(false);

        if (_lblMensaje.Text.Length > 0)
        {
            MostrarMensaje(string.Empty, EstiloUI.TextoSecundario);
        }
    }

    private void CancelarOCerrar()
    {
        if (_exito)
        {
            return;
        }

        if (_autenticando)
        {
            MostrarMensaje("Cancelando la operación...", EstiloUI.TextoSecundario);
            _cancelacion?.Cancel();
            return;
        }

        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void EstablecerModoCarga(bool enCurso)
    {
        _autenticando = enCurso;

        // El botón conserva el degradado y muestra el indicador de carga,
        // pero bloquea clics adicionales mientras la petición está en curso.
        _btnIngresar.EnProgreso = enCurso;
        EstablecerCamposHabilitados(!enCurso);

        if (enCurso)
        {
            MostrarMensaje("Verificando credenciales...", EstiloUI.TextoSecundario);
        }
    }

    private void EstablecerCamposHabilitados(bool habilitados)
    {
        _txtUsuario.Enabled = habilitados;
        _txtContrasena.Enabled = habilitados;
    }

    private void MostrarMensaje(string mensaje, Color color)
    {
        _lblMensaje.Text = mensaje;
        _lblMensaje.ForeColor = color;
    }
}
