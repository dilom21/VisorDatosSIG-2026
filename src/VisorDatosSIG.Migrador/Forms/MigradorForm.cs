using System.Text.Json;
using System.Windows.Forms;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Migration;
using VisorDatosSIG.Migrador.Services;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Ventana principal del Migrador.
/// Fase 1: selección, validación de archivos asociados e inspección del shapefile.
/// Fase 2: validación detallada (registro por registro) antes de cualquier migración.
/// Fase 3: migración transaccional (reemplazar o anexar) con auditoría y bitácora.
/// Fase 4: dashboard histórico de operaciones en SQL Server.
/// </summary>
public sealed partial class MigradorForm : Form
{
    private const string TextoSinValor = "-";

    private readonly IShapefileReader _shapefileReader;
    private readonly IShapefileValidator _shapefileValidator;
    private readonly ISqlServerConnectionProbe _connectionProbe;
    private readonly IMigrationService _migrationService;
    private readonly SqlMigrationWriter _migrationWriter;
    private readonly IMigrationExporter _migrationExporter;
    private readonly IBitacoraService _bitacoraService;
    private readonly AuthenticationApiClient _apiClient;
    private readonly UserSession _sesion;
    private readonly int _batchSize;

    private string? _ultimaCarpeta;
    private ShapefileInfoDto? _inspeccionActual;
    private ShapefileValidationResultDto? _validacionActual;
    private MigrationResult? _ultimoResultadoMigracion;
    private IReadOnlyList<RecordValidationIssueDto> _incidenciasActuales = [];
    private IReadOnlyList<BitacoraItemDto> _historialBitacora = [];
    private CancellationTokenSource? _cancelacionValidacion;
    private CancellationTokenSource? _cancelacionMigracion;
    private bool _validacionEnCurso;
    private bool _migracionEnCurso;

    /// <summary>
    /// Inicializa el formulario con los servicios de inspección, validación, migración, exportación, bitácora y sesión de usuario.
    /// </summary>
    public MigradorForm(
        IShapefileReader shapefileReader,
        IShapefileValidator shapefileValidator,
        ISqlServerConnectionProbe connectionProbe,
        IMigrationService migrationService,
        SqlMigrationWriter migrationWriter,
        IMigrationExporter migrationExporter,
        IBitacoraService bitacoraService,
        AuthenticationApiClient apiClient,
        UserSession sesion,
        int batchSize)
    {
        _shapefileReader = shapefileReader ?? throw new ArgumentNullException(nameof(shapefileReader));
        _shapefileValidator = shapefileValidator ?? throw new ArgumentNullException(nameof(shapefileValidator));
        _connectionProbe = connectionProbe ?? throw new ArgumentNullException(nameof(connectionProbe));
        _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
        _migrationWriter = migrationWriter ?? throw new ArgumentNullException(nameof(migrationWriter));
        _migrationExporter = migrationExporter ?? throw new ArgumentNullException(nameof(migrationExporter));
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _batchSize = batchSize > 0 ? batchSize : 500;

        InicializarInterfaz();
        InicializarSesion();
        ReiniciarResultados();
    }

    /// <summary>
    /// Libera los recursos propios del formulario, cancelando cualquier operación en curso.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancelacionValidacion?.Cancel();
            _cancelacionValidacion?.Dispose();
            _cancelacionValidacion = null;
            _cancelacionMigracion?.Cancel();
            _cancelacionMigracion?.Dispose();
            _cancelacionMigracion = null;
            LiberarSesion();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void cboModalidad_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var esAppend = _cboModalidad.SelectedIndex == 1;
        _btnMigrar.Text = esAppend ? "Migrar (anexar)" : "Migrar (reemplazar)";
    }

    private async void btnProbarConexion_Click(object? sender, EventArgs e)
    {
        try
        {
            _btnProbarConexion.Enabled = false;
            EstablecerActividad("Comprobando conexión con SQL Server...");
            var result = await _connectionProbe.TestAsync();
            MostrarResultadoConexion(result);
        }
        catch (Exception ex)
        {
            if (FormularioActivo)
            {
                MostrarErrorInesperado("No se pudo comprobar la conexión con SQL Server.", ex);
            }
        }
        finally
        {
            if (FormularioActivo)
            {
                _btnProbarConexion.Enabled = true;
                ActualizarAcciones();
            }
        }
    }

    private async void btnMigrar_Click(object? sender, EventArgs e)
    {
        try
        {
            await MigrarAsync();
        }
        catch (Exception ex)
        {
            if (FormularioActivo)
            {
                MostrarErrorInesperado("Ocurrió un error inesperado durante la migración.", ex);
            }
        }
    }

    private async void btnExportar_Click(object? sender, EventArgs e)
    {
        if (_inspeccionActual is null)
        {
            MessageBox.Show(this, "No hay datos para exportar.", "Exportar resumen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var dialogo = new SaveFileDialog
            {
                Title = "Exportar resumen e informe técnico",
                Filter = "Informe técnico (*.txt)|*.txt|Incidencias detalladas (*.csv)|*.csv",
                FileName = $"Resumen_Migracion_{_inspeccionActual.Layer}_{DateTime.Now:yyyyMMdd_HHmm}"
            };

            if (dialogo.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var extension = Path.GetExtension(dialogo.FileName).ToLowerInvariant();
            if (extension == ".csv")
            {
                await _migrationExporter.ExportarCsvAsync(dialogo.FileName, _inspeccionActual, _validacionActual, _ultimoResultadoMigracion);
            }
            else
            {
                await _migrationExporter.ExportarTextoAsync(dialogo.FileName, _inspeccionActual, _validacionActual, _ultimoResultadoMigracion);
            }

            EstablecerActividad($"Informe exportado a: {Path.GetFileName(dialogo.FileName)}");
            MessageBox.Show(
                this,
                $"Informe guardado correctamente en:{Environment.NewLine}{dialogo.FileName}",
                "Exportación exitosa",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MostrarErrorInesperado("No se pudo exportar el informe.", ex);
        }
    }

    private async void btnReconstruirIndices_Click(object? sender, EventArgs e)
    {
        try
        {
            _btnReconstruirIndices.Enabled = false;
            EstablecerActividad("Reconstruyendo índices espaciales en SQL Server...");
            var (succeeded, message) = await _migrationWriter.RebuildSpatialIndexesAsync();
            EstablecerActividad(message);
            MessageBox.Show(
                this,
                message,
                "Reconstrucción de índices espaciales",
                MessageBoxButtons.OK,
                succeeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MostrarErrorInesperado("Ocurrió un error al reconstruir los índices espaciales.", ex);
        }
        finally
        {
            if (FormularioActivo)
            {
                _btnReconstruirIndices.Enabled = true;
            }
        }
    }

    private async void btnRefrescarBitacora_Click(object? sender, EventArgs e)
    {
        await CargarBitacoraAsync();
    }

    private void cboFiltroBitacora_SelectedIndexChanged(object? sender, EventArgs e)
    {
        AplicarFiltroBitacora();
    }

    private void dgvBitacora_SelectionChanged(object? sender, EventArgs e)
    {
        if (_dgvBitacora.SelectedRows.Count == 0)
        {
            _txtDetalleBitacora.Text = "Seleccione una operación para ver su detalle.";
            return;
        }

        var fila = _dgvBitacora.SelectedRows[0];
        if (fila.Tag is BitacoraItemDto item)
        {
            MostrarDetalleBitacora(item);
        }
    }

    public async Task CargarBitacoraAsync()
    {
        try
        {
            _btnRefrescarBitacora.Enabled = false;
            _lblResumenBitacora.Text = "Consultando dbo.Bitacora...";
            var historial = await _bitacoraService.ObtenerHistorialAsync(100);
            _historialBitacora = historial;

            var total = historial.Count;
            var exitos = historial.Count(h => h.Resultado.Equals("EXITO", StringComparison.OrdinalIgnoreCase));
            var errores = historial.Count(h => !h.Resultado.Equals("EXITO", StringComparison.OrdinalIgnoreCase));
            var ultima = historial.Count > 0 ? historial[0].FechaHora.ToLocalTime().ToString("dd/MM HH:mm") : TextoSinValor;

            _cardBitacoraTotal.Set("Operaciones", total.ToString("N0"), ColorMarca);
            _cardBitacoraExitos.Set("Migraciones exitosas", exitos.ToString("N0"), ColorOk);
            _cardBitacoraErrores.Set("Errores / Cancelados", errores.ToString("N0"), errores > 0 ? ColorError : ColorInfo);
            _cardBitacoraUltima.Set("Última actividad", ultima, ColorTexto);

            AplicarFiltroBitacora();
        }
        catch (Exception ex)
        {
            _lblResumenBitacora.Text = $"Error al consultar bitácora: {ex.Message}";
        }
        finally
        {
            if (FormularioActivo)
            {
                _btnRefrescarBitacora.Enabled = true;
            }
        }
    }

    private void AplicarFiltroBitacora()
    {
        var tablaFiltro = _cboFiltroBitacoraTabla.SelectedItem?.ToString() ?? "Todas";
        var resultadoFiltro = _cboFiltroBitacoraResultado.SelectedItem?.ToString() ?? "Todos";

        var filtradas = _historialBitacora
            .Where(item => tablaFiltro == "Todas" || item.Entidad.Equals(tablaFiltro, StringComparison.OrdinalIgnoreCase))
            .Where(item => resultadoFiltro == "Todos" || item.Resultado.Equals(resultadoFiltro, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _dgvBitacora.SuspendLayout();
        _dgvBitacora.Rows.Clear();

        foreach (var item in filtradas)
        {
            var idx = _dgvBitacora.Rows.Add(
                item.IdBitacora,
                item.FechaHora.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                item.Modulo,
                item.Accion,
                item.Entidad,
                item.Resultado,
                item.IP ?? "-");

            var fila = _dgvBitacora.Rows[idx];
            fila.Tag = item;

            var celdaRes = fila.Cells["colBitacoraResultado"];
            if (item.Resultado.Equals("EXITO", StringComparison.OrdinalIgnoreCase))
            {
                celdaRes.Style.ForeColor = ColorOk;
                celdaRes.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            else if (item.Resultado.Equals("CANCELADO", StringComparison.OrdinalIgnoreCase))
            {
                celdaRes.Style.ForeColor = ColorAdvertencia;
            }
            else
            {
                celdaRes.Style.ForeColor = ColorError;
                celdaRes.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
        }

        _dgvBitacora.ResumeLayout();
        _lblResumenBitacora.Text = $"Mostrando {filtradas.Count} de {_historialBitacora.Count} operaciones registradas.";

        if (_dgvBitacora.Rows.Count > 0)
        {
            _dgvBitacora.Rows[0].Selected = true;
            if (_dgvBitacora.Rows[0].Tag is BitacoraItemDto primerItem)
            {
                MostrarDetalleBitacora(primerItem);
            }
        }
        else
        {
            _txtDetalleBitacora.Text = "No hay registros para mostrar con los filtros aplicados.";
        }
    }

    private void MostrarDetalleBitacora(BitacoraItemDto item)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Operación ID: #{item.IdBitacora}  |  Fecha: {item.FechaHora.ToLocalTime():yyyy-MM-dd HH:mm:ss}  |  Estado: {item.Resultado}");
        sb.AppendLine($"Módulo: {item.Modulo}  |  Acción: {item.Accion}  |  Tabla: {item.Entidad}  |  Equipo: {item.IP ?? Environment.MachineName}");
        sb.AppendLine(new string('-', 85));

        if (!string.IsNullOrWhiteSpace(item.Detalle))
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(item.Detalle);
                sb.AppendLine("Resumen estructurado:");
                foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine($"  {prop.Name}:");
                        foreach (var elem in prop.Value.EnumerateArray())
                        {
                            sb.AppendLine($"    • {elem.GetString()}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"  {prop.Name,-18}: {prop.Value}");
                    }
                }
            }
            catch
            {
                sb.AppendLine("Detalle:");
                sb.AppendLine(item.Detalle);
            }
        }

        _txtDetalleBitacora.Text = sb.ToString();
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
        var cancellation = _cancelacionValidacion ?? _cancelacionMigracion;
        if (cancellation is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        EstablecerActividad(_migracionEnCurso ? "Cancelando la migración..." : "Cancelando la validación...");
        _btnCancelar.Enabled = false;
        cancellation.Cancel();
    }

    private async Task MigrarAsync()
    {
        var info = _inspeccionActual;
        var validation = _validacionActual;
        if (info is null || validation is null || !PuedeValidar(info) || validation.HasErrors)
        {
            EstablecerActividad("Debe inspeccionar y validar correctamente el archivo antes de migrar.");
            return;
        }

        var mode = _cboModalidad.SelectedIndex == 1 ? MigrationMode.Append : MigrationMode.Replace;
        var mensajeConfirmacion = mode == MigrationMode.Replace
            ? "La modalidad Reemplazar eliminará los datos actuales de la tabla de destino dentro de una transacción. " +
              "Si la carga falla, se ejecutará rollback. ¿Desea continuar?"
            : "La modalidad Anexar conservará los datos existentes en SQL Server e insertará únicamente los registros " +
              "que no estén duplicados por su clave natural. ¿Desea continuar?";

        var confirmation = MessageBox.Show(
            this,
            mensajeConfirmacion,
            "Confirmar migración",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        _cancelacionMigracion = new CancellationTokenSource();
        try
        {
            ActivarModoMigracion(true);
            var progress = new Progress<MigrationProgress>(MostrarProgresoMigracion);
            var result = await _migrationService.MigrateAsync(
                new MigrationOptions
                {
                    ShapefilePath = info.FilePath,
                    Layer = info.Layer,
                    Mode = mode,
                    BatchSize = _batchSize
                },
                progress,
                _cancelacionMigracion.Token);

            _ultimoResultadoMigracion = result;

            if (FormularioActivo)
            {
                MostrarResultadoMigracion(result);
                _btnExportar.Enabled = true;
                _ = CargarBitacoraAsync();
            }
        }
        finally
        {
            _cancelacionMigracion?.Dispose();
            _cancelacionMigracion = null;
            if (FormularioActivo)
            {
                ActivarModoMigracion(false);
            }
        }
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
            _btnExportar.Enabled = true;
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

    private void ActivarModoMigracion(bool enCurso)
    {
        _migracionEnCurso = enCurso;
        _btnSeleccionar.Enabled = !enCurso;
        _btnValidar.Enabled = !enCurso && _inspeccionActual is not null && PuedeValidar(_inspeccionActual);
        _cboModalidad.Enabled = !enCurso;
        _btnMigrar.Enabled = !enCurso && _validacionActual is not null && !_validacionActual.HasErrors;
        _btnCancelar.Enabled = enCurso;
        _btnExportar.Enabled = !enCurso && (_validacionActual is not null || _ultimoResultadoMigracion is not null);
        _btnReconstruirIndices.Enabled = !enCurso;
        _btnProbarConexion.Enabled = !enCurso;
        _barraProgreso.Visible = enCurso;
        _pnlProgreso.Visible = enCurso;
        UseWaitCursor = enCurso;
    }

    private void ActivarModoValidacion(bool enCurso)
    {
        _validacionEnCurso = enCurso;

        _btnSeleccionar.Enabled = !enCurso;
        _btnValidar.Enabled = !enCurso && _inspeccionActual is not null && PuedeValidar(_inspeccionActual);
        _cboModalidad.Enabled = !enCurso;
        _btnCancelar.Enabled = enCurso;
        _btnExportar.Enabled = !enCurso && (_validacionActual is not null || _ultimoResultadoMigracion is not null);
        _btnReconstruirIndices.Enabled = !enCurso;
        _barraProgreso.Visible = enCurso;
        _pnlProgreso.Visible = enCurso;
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
        _btnMigrar.Enabled = _validacionActual is not null
                             && !_validacionActual.HasErrors
                             && !_validacionEnCurso
                             && !_migracionEnCurso;
        _btnExportar.Enabled = _inspeccionActual is not null
                              && !_validacionEnCurso
                              && !_migracionEnCurso;
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
        else if (e.TabPage == _tabBitacora)
        {
            _ = CargarBitacoraAsync();
        }
    }

    private void cboFiltroIncidencias_SelectedIndexChanged(object? sender, EventArgs e) => AplicarFiltroIncidencias();

    private void ReiniciarResultados()
    {
        _inspeccionActual = null;
        _validacionActual = null;
        _ultimoResultadoMigracion = null;
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
        _pnlProgreso.Visible = false;
        _pgbProgresoGrande.Value = 0;
        _lblProgresoPorcentaje.Text = "0 %";
        _btnValidar.Enabled = false;
        _btnMigrar.Enabled = false;
        _btnCancelar.Enabled = false;
        _btnExportar.Enabled = false;

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
