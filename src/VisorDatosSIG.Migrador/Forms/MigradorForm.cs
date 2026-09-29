using System.Windows.Forms;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Migrador.Services;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Ventana principal del Migrador.
/// Fase 1: selección, validación de archivos asociados e inspección del shapefile.
/// Fase 2: validación detallada (registro por registro) antes de cualquier migración.
/// </summary>
/// <remarks>
/// El formulario no lee archivos ni conoce NetTopologySuite: delega la inspección en
/// <see cref="IShapefileReader"/> y la validación en <see cref="IShapefileValidator"/>,
/// cuyos contratos están en Application y cuyas implementaciones están en Infrastructure.
/// </remarks>
public sealed partial class MigradorForm : Form
{
    private const string TextoSinValor = "-";

    private readonly IShapefileReader _shapefileReader;
    private readonly IShapefileValidator _shapefileValidator;
    private readonly AuthenticationApiClient _apiClient;
    private readonly UserSession _sesion;

    private string? _ultimaCarpeta;
    private ShapefileInfoDto? _inspeccionActual;
    private ShapefileValidationResultDto? _validacionActual;
    private IReadOnlyList<RecordValidationIssueDto> _incidenciasActuales = [];
    private CancellationTokenSource? _cancelacionValidacion;
    private bool _validacionEnCurso;

    /// <summary>
    /// Inicializa el formulario con los servicios de inspección y validación y con la sesión del usuario.
    /// </summary>
    /// <param name="shapefileReader">Servicio de inspección de shapefiles.</param>
    /// <param name="shapefileValidator">Servicio de validación detallada.</param>
    /// <param name="apiClient">Cliente HTTP de autenticación (el Migrador nunca accede a SQL Server).</param>
    /// <param name="sesion">Sesión en memoria del usuario autenticado.</param>
    public MigradorForm(
        IShapefileReader shapefileReader,
        IShapefileValidator shapefileValidator,
        AuthenticationApiClient apiClient,
        UserSession sesion)
    {
        _shapefileReader = shapefileReader ?? throw new ArgumentNullException(nameof(shapefileReader));
        _shapefileValidator = shapefileValidator ?? throw new ArgumentNullException(nameof(shapefileValidator));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));

        InicializarInterfaz();
        InicializarSesion();
        ReiniciarResultados();
    }

    /// <summary>
    /// Libera los recursos propios del formulario, cancelando cualquier validación en curso.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancelacionValidacion?.Cancel();
            _cancelacionValidacion?.Dispose();
            _cancelacionValidacion = null;
            LiberarSesion();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void btnSeleccionar_Click(object? sender, EventArgs e)
    {
        try
        {
            using var dialogo = CrearDialogoSeleccion();
            if (dialogo.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _ultimaCarpeta = Path.GetDirectoryName(dialogo.FileName);
            _txtRuta.Text = dialogo.FileName;

            await InspeccionarAsync(dialogo.FileName);
        }
        catch (Exception ex)
        {
            if (FormularioActivo)
            {
                MostrarErrorInesperado("Ocurrió un error inesperado al seleccionar el archivo.", ex);
            }
        }
    }

    private OpenFileDialog CrearDialogoSeleccion()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Seleccione el archivo Shapefile (.shp)",
            Filter = "Shapefile (*.shp)|*.shp|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true
        };

        if (!string.IsNullOrWhiteSpace(_ultimaCarpeta))
        {
            dialogo.InitialDirectory = _ultimaCarpeta;
        }

        return dialogo;
    }

    private async Task InspeccionarAsync(string rutaShp)
    {
        EstablecerActividad("Leyendo el shapefile y validando los archivos asociados...");
        AlternarControles(false);
        ReiniciarResultados();
        _txtRuta.Text = rutaShp;

        try
        {
            var info = await _shapefileReader.InspectAsync(rutaShp);
            if (!FormularioActivo)
            {
                return;
            }

            MostrarResultadoInspeccion(info);
            ActualizarAcciones();
        }
        catch (Exception ex)
        {
            if (FormularioActivo)
            {
                MostrarErrorInesperado("No se pudo inspeccionar el archivo seleccionado.", ex);
            }
        }
        finally
        {
            if (FormularioActivo)
            {
                AlternarControles(true);
            }
        }
    }

    private async void btnValidar_Click(object? sender, EventArgs e)
    {
        try
        {
            await ValidarAsync();
        }
        catch (Exception ex)
        {
            if (FormularioActivo)
            {
                MostrarErrorInesperado("Ocurrió un error inesperado al validar los datos.", ex);
            }
        }
    }

    private void btnCancelar_Click(object? sender, EventArgs e)
    {
        if (_cancelacionValidacion is null || _cancelacionValidacion.IsCancellationRequested)
        {
            return;
        }

        EstablecerActividad("Cancelando la validación...");
        _btnCancelar.Enabled = false;
        _cancelacionValidacion.Cancel();
    }

    private async Task ValidarAsync()
    {
        var info = _inspeccionActual;
        if (info is null || !PuedeValidar(info))
        {
            EstablecerActividad("La inspección actual no permite ejecutar la validación.");
            return;
        }

        _cancelacionValidacion = new CancellationTokenSource();
        var progreso = new Progress<ValidationProgressDto>(MostrarProgresoValidacion);

        try
        {
            ActivarModoValidacion(true);

            var resultado = await _shapefileValidator.ValidateAsync(
                info.FilePath,
                info.Layer,
                progreso,
                _cancelacionValidacion.Token);

            if (!FormularioActivo)
            {
                return;
            }

            MostrarResultadoValidacion(resultado);
        }
        catch (OperationCanceledException)
        {
            if (FormularioActivo)
            {
                MostrarValidacionCancelada();
            }
        }
        catch (Exception ex)
        {
            if (FormularioActivo)
            {
                MostrarErrorInesperado("No se pudo validar el archivo seleccionado.", ex);
            }
        }
        finally
        {
            _cancelacionValidacion?.Dispose();
            _cancelacionValidacion = null;

            if (FormularioActivo)
            {
                ActivarModoValidacion(false);
            }
        }
    }

    private void ActivarModoValidacion(bool enCurso)
    {
        _validacionEnCurso = enCurso;

        _btnSeleccionar.Enabled = !enCurso;
        _btnValidar.Enabled = !enCurso && _inspeccionActual is not null && PuedeValidar(_inspeccionActual);
        _btnCancelar.Enabled = enCurso;
        _barraProgreso.Visible = enCurso;
        UseWaitCursor = enCurso;

        if (enCurso)
        {
            _barraProgreso.Value = 0;
            _lblEstadoValidacion.Text = "Validando todos los registros del shapefile...";
            _lblEstadoValidacion.BackColor = ColorMarcaClara;

            if (_tabs.SelectedTab != _tabValidacion)
            {
                _tabs.SelectedTab = _tabValidacion;
            }
        }
        else
        {
            _barraProgreso.Value = 0;
            _barraProgreso.Visible = false;
        }
    }

    private void ActualizarAcciones()
    {
        _btnValidar.Enabled = _inspeccionActual is not null
                              && PuedeValidar(_inspeccionActual)
                              && !_validacionEnCurso;
    }

    private void tabs_Selecting(object? sender, TabControlCancelEventArgs e)
    {
        if (e.TabPage == _tabValidacion && _validacionActual is null && !_validacionEnCurso)
        {
            e.Cancel = true;
            EstablecerActividad("Pulse «Validar datos» para ver el resumen de la validación.");
        }
        else if (e.TabPage == _tabIncidencias && _validacionActual is null)
        {
            e.Cancel = true;
            EstablecerActividad("Pulse «Validar datos» para ver las incidencias.");
        }
        else if (e.TabPage == _tabPrevisualizacion && _inspeccionActual is null)
        {
            e.Cancel = true;
            EstablecerActividad("Seleccione un archivo .shp para ver la previsualización.");
        }
    }

    private void cboFiltroIncidencias_SelectedIndexChanged(object? sender, EventArgs e) => AplicarFiltroIncidencias();

    private void ReiniciarResultados()
    {
        _inspeccionActual = null;
        _validacionActual = null;
        _incidenciasActuales = [];

        _cardCapa.Set("Capa detectada", TextoSinValor, ColorMarca);
        _cardRegistros.Set("Registros", TextoSinValor, ColorMarca);
        _cardReferencia.Set("SRID / Referencia", TextoSinValor, ColorAdvertencia);
        _cardEstado.Set("Estado", TextoSinValor, ColorTexto);
        _cardAnalizados.Set("Analizados", TextoSinValor, ColorInfo);
        _cardValidos.Set("Válidos para migrar", TextoSinValor, ColorOk);
        _cardAdvertencias.Set("Registros con advertencias", TextoSinValor, ColorAdvertencia);
        _cardOmitibles.Set("Omitibles", TextoSinValor, ColorError);

        _lblValorArchivo.Text = TextoSinValor;
        _lblValorCapa.Text = TextoSinValor;
        _lblValorCapa.ForeColor = ColorTexto;
        _lblValorRegistros.Text = TextoSinValor;
        _lblValorGeometria.Text = TextoSinValor;
        _lblValorReferencia.Text = TextoSinValor;
        _lblValorExtension.Text = TextoSinValor;
        _lblValorCodificacion.Text = TextoSinValor;
        _lblValorEstado.Text = TextoSinValor;
        _lblValorEstado.ForeColor = ColorTexto;

        foreach (var etiqueta in _lblComponentes.Values)
        {
            etiqueta.Text = TextoSinValor;
            etiqueta.ForeColor = ColorTexto;
        }

        _lstCampos.Items.Clear();
        _dgvPrevisualizacion.Rows.Clear();
        _dgvPrevisualizacion.Columns.Clear();
        _lblResumenPrevisualizacion.Text = "Sin datos: seleccione un archivo .shp.";
        _dgvEstadisticas.Rows.Clear();
        _dgvIncidencias.Rows.Clear();
        _cboFiltroIncidencias.Items.Clear();
        _lblResumenIncidencias.Text = "Sin incidencias registradas.";
        _lblEstadoValidacion.Text = "Sin validación ejecutada. Seleccione un archivo .shp y pulse «Validar datos».";
        _lblEstadoValidacion.BackColor = ColorMarcaClara;
        _barraProgreso.Value = 0;
        _barraProgreso.Visible = false;
        _btnValidar.Enabled = false;
        _btnCancelar.Enabled = false;

        EstablecerMensajes(["Seleccione un archivo .shp para inspeccionarlo."]);

        if (_tabs.SelectedTab != _tabInspeccion)
        {
            _tabs.SelectedTab = _tabInspeccion;
        }
    }

    private void AlternarControles(bool habilitado)
    {
        _btnSeleccionar.Enabled = habilitado;
        UseWaitCursor = !habilitado;

        if (habilitado)
        {
            ActualizarAcciones();
        }
    }

    private void EstablecerActividad(string texto) => _lblActividad.Text = texto;

    private bool FormularioActivo => !IsDisposed && !Disposing;

    private static bool PuedeValidar(ShapefileInfoDto info) =>
        info.Status != ShapefileInspectionStatus.Failed
        && info.Layer != ShapefileLayer.Unrecognized
        && info.RecordCount > 0;
}
