using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Presentación de resultados: información de la inspección, validación e incidencias.
/// </summary>
public sealed partial class MigradorForm
{
    private void MostrarResultadoInspeccion(ShapefileInfoDto info)
    {
        _inspeccionActual = info;

        _cardCapa.Set("Capa detectada", info.LayerDisplayName, info.Layer == ShapefileLayer.Unrecognized ? ColorError : ColorMarca);
        _cardRegistros.Set("Registros", FormatearNumero(info.RecordCount), ColorMarca);
        _cardReferencia.Set("SRID / Referencia", ComponerTextoReferenciaCorto(info.SpatialReference), ColorParaReferencia(info.SpatialReference));
        MostrarEstadoInspeccion();

        _cardAnalizados.Set("Analizados", TextoSinValor, ColorInfo);
        _cardValidos.Set("Válidos para migrar", TextoSinValor, ColorOk);
        _cardAdvertencias.Set("Registros con advertencias", TextoSinValor, ColorAdvertencia);
        _cardOmitibles.Set("Omitibles", TextoSinValor, ColorError);

        _lblValorArchivo.Text = info.FileName;
        _toolTip.SetToolTip(_lblValorArchivo, info.FilePath);

        _lblValorCapa.Text = info.LayerDisplayName;
        _lblValorCapa.ForeColor = info.Layer == ShapefileLayer.Unrecognized ? ColorError : ColorTexto;

        _lblValorRegistros.Text = FormatearNumero(info.RecordCount);
        _toolTip.SetToolTip(
            _lblValorRegistros,
            $"Registros declarados por el archivo DBF: {info.RecordCount:N0}. " +
            $"Registros previsualizados: {info.Preview.Records.Count:N0}.");

        _lblValorGeometria.Text = ComponerTextoGeometria(info);
        _toolTip.SetToolTip(_lblValorGeometria, ComponerTextoGeometria(info));

        _lblValorReferencia.Text = ComponerTextoReferencia(info.SpatialReference);
        _toolTip.SetToolTip(_lblValorReferencia, ComponerDetalleReferencia(info.SpatialReference));

        _lblValorExtension.Text = info.ExtentText ?? "No disponible";
        _toolTip.SetToolTip(
            _lblValorExtension,
            info.ExtentWithinGeographicRange switch
            {
                true => "Las coordenadas están dentro del rango geográfico válido (longitud -180..180, latitud -90..90).",
                false => "Las coordenadas están fuera del rango geográfico válido: el archivo podría no estar en un sistema geográfico.",
                _ => "No fue posible determinar la extensión del archivo."
            });

        _lblValorCodificacion.Text = info.DbfEncodingName ?? "No disponible";

        _lblValorEstado.Text = $"{info.Status.ToDisplayName()} - {info.StatusMessage}";
        _lblValorEstado.ForeColor = ColorParaEstado(info.Status);
        _toolTip.SetToolTip(_lblValorEstado, _lblValorEstado.Text);

        MostrarComponentes(info.Components);
        MostrarCampos(info.Fields);
        MostrarPrevisualizacion(info.Preview);
        MostrarMensajesInspeccion(info);

        var puedeValidar = PuedeValidar(info);
        _lblEstadoValidacion.Text = puedeValidar
            ? "Inspección correcta. Pulse «Validar datos» para analizar todos los registros y detectar incidencias."
            : "La inspección no permite continuar con la validación.";
        _lblEstadoValidacion.BackColor = puedeValidar ? ColorMarcaClara : ColorSuaveError;

        EstablecerActividad(info.Status switch
        {
            ShapefileInspectionStatus.Failed => "No se pudo inspeccionar el archivo.",
            ShapefileInspectionStatus.Warning =>
                $"Inspección finalizada con advertencias: {info.RecordCount:N0} registro(s).",
            _ =>
                $"Inspección finalizada correctamente: {info.RecordCount:N0} registro(s)."
        });

        if (_tabs.SelectedTab != _tabInspeccion)
        {
            _tabs.SelectedTab = _tabInspeccion;
        }
    }

    private void MostrarComponentes(IReadOnlyList<ShapefileComponentDto> componentes)
    {
        foreach (var componente in componentes)
        {
            if (!_lblComponentes.TryGetValue(componente.Type, out var etiqueta))
            {
                continue;
            }

            etiqueta.Text = componente.Exists
                ? "Presente"
                : componente.IsRequired ? "FALTANTE" : "FALTANTE (opcional)";

            etiqueta.ForeColor = componente.Exists
                ? ColorOk
                : componente.IsRequired ? ColorError : ColorAdvertencia;

            _toolTip.SetToolTip(etiqueta, componente.FilePath);
        }
    }

    private void MostrarCampos(IReadOnlyList<ShapefileFieldDto> campos)
    {
        _lstCampos.BeginUpdate();
        _lstCampos.Items.Clear();

        foreach (var campo in campos)
        {
            var item = new ListViewItem(campo.Name);
            item.SubItems.Add(campo.DataType);
            item.SubItems.Add(campo.Length.ToString(CultureInfo.CurrentCulture));
            item.SubItems.Add(campo.DecimalCount.ToString(CultureInfo.CurrentCulture));
            item.ToolTipText = $"{campo.Name} ({campo.DisplayType})";
            _lstCampos.Items.Add(item);
        }

        _lstCampos.EndUpdate();
    }

    private void MostrarPrevisualizacion(ShapefilePreviewDto previsualizacion)
    {
        _dgvPrevisualizacion.Rows.Clear();
        _dgvPrevisualizacion.Columns.Clear();

        var total = _inspeccionActual?.RecordCount ?? 0;
        _lblResumenPrevisualizacion.Text = previsualizacion.Records.Count == 0
            ? "Sin registros para previsualizar."
            : $"Se muestran {FormatearNumero(previsualizacion.Records.Count)} de {FormatearNumero(total)} registro(s)" +
              (previsualizacion.IsLimited ? " (vista limitada a 20)." : " (archivo completo).");

        if (previsualizacion.Records.Count == 0)
        {
            return;
        }

        _dgvPrevisualizacion.Columns.Add("columnaRegistro", "N°");
        _dgvPrevisualizacion.Columns.Add("columnaGeometria", "Geometría");

        for (var indice = 0; indice < previsualizacion.ColumnNames.Count; indice++)
        {
            _dgvPrevisualizacion.Columns.Add($"columna{indice}", previsualizacion.ColumnNames[indice]);
        }

        foreach (var registro in previsualizacion.Records)
        {
            var valores = new List<object>(previsualizacion.ColumnNames.Count + 2)
            {
                registro.RecordNumber,
                registro.GeometryType
            };

            valores.AddRange(previsualizacion.ColumnNames.Select(
                nombre => (object)(registro.GetValue(nombre) ?? string.Empty)));

            _dgvPrevisualizacion.Rows.Add(valores.ToArray());
        }
    }

    private void MostrarMensajesInspeccion(ShapefileInfoDto info)
    {
        var lineas = new List<string>(info.Errors.Count + info.Warnings.Count + 1);
        lineas.AddRange(info.Errors.Select(mensaje => "ERROR: " + mensaje));
        lineas.AddRange(info.Warnings.Select(mensaje => "ADVERTENCIA: " + mensaje));

        if (lineas.Count == 0)
        {
            lineas.Add("Sin errores ni advertencias.");
        }

        EstablecerMensajes(lineas);
    }

    private void EstablecerMensajes(IEnumerable<string> lineas)
    {
        _txtMensajes.Lines = lineas.ToArray();
        _txtMensajes.SelectionStart = 0;
        _txtMensajes.SelectionLength = 0;
    }

    private void MostrarResultadoValidacion(ShapefileValidationResultDto resultado)
    {
        _validacionActual = resultado;

        var resumen = resultado.Summary;

        // La tarjeta de estado refleja el resultado de la validación una vez ejecutada.
        _cardEstado.Set("Estado", ComponerEstadoValidacion(resultado), ColorParaEstadoValidacion(resultado));
        _cardAnalizados.Set("Analizados", FormatearNumero(resumen.AnalyzedRecords), ColorInfo);
        _cardValidos.Set("Válidos para migrar", FormatearNumero(resumen.ValidRecords), ColorOk);
        _cardAdvertencias.Set(
            "Registros con advertencias",
            FormatearNumero(resumen.RecordsWithWarnings),
            resumen.RecordsWithWarnings > 0 ? ColorAdvertencia : ColorOk);
        _cardOmitibles.Set(
            "Omitibles",
            FormatearNumero(resumen.OmitableRecords),
            resumen.OmitableRecords > 0 ? ColorError : ColorOk);

        var detalles = new List<string>
        {
            resultado.StatusMessage,
            $"Duración: {FormatearDuracion(resultado.Duration)}",
            $"Candidatos a migración: {FormatearNumero(resumen.MigrationCandidateRecords)}"
        };

        if (resultado.Errors.Count > 0)
        {
            detalles.Add($"Errores técnicos: {resultado.Errors.Count:N0}");
        }

        _lblEstadoValidacion.Text = string.Join("  ·  ", detalles);
        _lblEstadoValidacion.BackColor = resultado.Status switch
        {
            ShapefileInspectionStatus.Failed => ColorSuaveError,
            ShapefileInspectionStatus.Warning => ColorSuaveAdvertencia,
            _ => ColorMarcaClara
        };

        MostrarEstadisticas(resultado.Statistics);
        MostrarIncidencias(resultado.Issues);

        EstablecerActividad(ComponerMensajeValidacion(resultado));

        if (_tabs.SelectedTab != _tabValidacion)
        {
            _tabs.SelectedTab = _tabValidacion;
        }
    }

    private void MostrarValidacionCancelada()
    {
        _validacionActual = null;

        _dgvEstadisticas.Rows.Clear();
        _dgvIncidencias.Rows.Clear();
        _cboFiltroIncidencias.Items.Clear();
        _lblResumenIncidencias.Text = "Sin incidencias registradas.";

        _cardAnalizados.Set("Analizados", TextoSinValor, ColorInfo);
        _cardValidos.Set("Válidos para migrar", TextoSinValor, ColorOk);
        _cardAdvertencias.Set("Registros con advertencias", TextoSinValor, ColorAdvertencia);
        _cardOmitibles.Set("Omitibles", TextoSinValor, ColorError);
        MostrarEstadoInspeccion();

        _lblEstadoValidacion.Text = "Validación cancelada por el usuario. Puede volver a ejecutarla cuando lo desee.";
        _lblEstadoValidacion.BackColor = ColorMarcaClara;

        EstablecerActividad("Validación cancelada por el usuario.");
    }

    private void MostrarEstadisticas(IReadOnlyList<ValidationStatisticDto> estadisticas)
    {
        _dgvEstadisticas.SuspendLayout();
        _dgvEstadisticas.Rows.Clear();

        foreach (var estadistica in estadisticas)
        {
            var indice = _dgvEstadisticas.Rows.Add(estadistica.Label, estadistica.Value, estadistica.Note ?? string.Empty);
            var celda = _dgvEstadisticas.Rows[indice].Cells[1];
            celda.Style.ForeColor = ColorParaSeveridad(estadistica.Severity);
            celda.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            if (estadistica.Severity != ValidationSeverity.Info)
            {
                celda.Style.BackColor = ColorSuaveParaSeveridad(estadistica.Severity);
            }
        }

        _dgvEstadisticas.ResumeLayout();
    }

    private void MostrarProgresoValidacion(ValidationProgressDto progreso)
    {
        _barraProgreso.Maximum = Math.Max(1, progreso.TotalRecords);
        _barraProgreso.Value = Math.Clamp(progreso.ProcessedRecords, 0, _barraProgreso.Maximum);

        EstablecerActividad(
            $"{progreso.Phase}: {progreso.ProcessedRecords:N0} de {progreso.TotalRecords:N0} ({progreso.Percentage} %)...");
    }

    private void MostrarErrorInesperado(string mensaje, Exception excepcion)
    {
        EstablecerMensajes(
        [
            "ERROR: " + mensaje,
            $"Detalle técnico: {excepcion.GetType().Name}: {excepcion.Message}"
        ]);

        EstablecerActividad("Error durante la operación.");

        MessageBox.Show(
            this,
            $"{mensaje}{Environment.NewLine}{Environment.NewLine}" +
            $"Detalle técnico: {excepcion.GetType().Name}: {excepcion.Message}",
            "Migrador - Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void MostrarIncidencias(IReadOnlyList<RecordValidationIssueDto> incidencias)
    {
        _incidenciasActuales = incidencias;
        ActualizarOpcionesFiltro(incidencias);
        AplicarFiltroIncidencias();
    }

    private void ActualizarOpcionesFiltro(IReadOnlyList<RecordValidationIssueDto> incidencias)
    {
        var seleccionPrevia = _cboFiltroIncidencias.SelectedIndex;

        _cboFiltroIncidencias.Items.Clear();
        _cboFiltroIncidencias.Items.Add($"Todas ({incidencias.Count:N0})");
        _cboFiltroIncidencias.Items.Add(
            $"Omitibles ({incidencias.Count(incidencia => incidencia.Disposition == RecordDisposition.Omit):N0})");
        _cboFiltroIncidencias.Items.Add(
            $"Solo errores ({incidencias.Count(incidencia => incidencia.Severity == ValidationSeverity.Error):N0})");
        _cboFiltroIncidencias.Items.Add(
            $"Solo advertencias ({incidencias.Count(incidencia => incidencia.Severity == ValidationSeverity.Warning):N0})");
        _cboFiltroIncidencias.Items.Add(
            $"Solo información ({incidencias.Count(incidencia => incidencia.Severity == ValidationSeverity.Info):N0})");

        _cboFiltroIncidencias.SelectedIndex = seleccionPrevia is >= 0 and < 5 ? seleccionPrevia : 0;
    }

    private void AplicarFiltroIncidencias()
    {
        var filtro = ObtenerFiltroSeleccionado();
        var visibles = _incidenciasActuales.Where(filtro).ToList();

        _dgvIncidencias.SuspendLayout();
        _dgvIncidencias.Rows.Clear();

        foreach (var incidencia in visibles)
        {
            var indice = _dgvIncidencias.Rows.Add(
                incidencia.RecordNumber > 0
                    ? incidencia.RecordNumber.ToString("N0", CultureInfo.CurrentCulture)
                    : "—",
                incidencia.Severity.ToDisplayName(),
                incidencia.IssueType.ToDisplayName(),
                incidencia.Field ?? "—",
                incidencia.Value ?? "—",
                incidencia.Disposition.ToDisplayName(),
                incidencia.Message,
                incidencia.RecommendedAction);

            var fila = _dgvIncidencias.Rows[indice];

            var celdaSeveridad = fila.Cells[1];
            celdaSeveridad.Style.ForeColor = ColorParaSeveridad(incidencia.Severity);
            celdaSeveridad.Style.BackColor = ColorSuaveParaSeveridad(incidencia.Severity);
            celdaSeveridad.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            var celdaAccion = fila.Cells[5];
            celdaAccion.Style.ForeColor = ColorParaDisposicion(incidencia.Disposition);
            celdaAccion.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        }

        _dgvIncidencias.ResumeLayout();

        var omitibles = visibles.Count(incidencia => incidencia.Disposition == RecordDisposition.Omit);

        _lblResumenIncidencias.Text = _validacionActual is null
            ? "Sin incidencias registradas."
            : visibles.Count == 0
                ? "Sin incidencias para el filtro seleccionado."
                : $"Mostrando {FormatearNumero(visibles.Count)} de {FormatearNumero(_incidenciasActuales.Count)} " +
                  $"incidencia(s) · Omitibles mostrados: {FormatearNumero(omitibles)}";
    }

    private Func<RecordValidationIssueDto, bool> ObtenerFiltroSeleccionado() => _cboFiltroIncidencias.SelectedIndex switch
    {
        1 => incidencia => incidencia.Disposition == RecordDisposition.Omit,
        2 => incidencia => incidencia.Severity == ValidationSeverity.Error,
        3 => incidencia => incidencia.Severity == ValidationSeverity.Warning,
        4 => incidencia => incidencia.Severity == ValidationSeverity.Info,
        _ => incidencia => true
    };

    /// <summary>
    /// Compone el mensaje final de la validación, conservando siempre el conteo de
    /// observaciones informativas y distinguiendo las incidencias bloqueantes.
    /// </summary>
    private static string ComponerMensajeValidacion(ShapefileValidationResultDto resultado)
    {
        var resumen = resultado.Summary;

        if (resultado.Status == ShapefileInspectionStatus.Failed)
        {
            return $"Validación incompleta: {resultado.Errors.FirstOrDefault() ?? resultado.StatusMessage}";
        }

        var analizados = Conteo(resumen.AnalyzedRecords, "registro analizado", "registros analizados");
        var informativas = Conteo(resumen.InfoIssueCount, "observación informativa", "observaciones informativas");
        var omitibles = Conteo(resumen.OmitableRecords, "omitible", "omitibles");

        if (resumen.ErrorIssueCount > 0 || resumen.OmitableRecords > 0)
        {
            return $"Validación completada con incidencias: {analizados}, " +
                   $"{Conteo(resumen.ErrorIssueCount, "error", "errores")}, " +
                   $"{Conteo(resumen.WarningIssueCount, "advertencia", "advertencias")}, " +
                   $"{informativas}, {omitibles}.";
        }

        var advertencias = resumen.WarningIssueCount > 0
            ? $" {Conteo(resumen.WarningIssueCount, "advertencia registrada", "advertencias registradas")}."
            : string.Empty;

        return $"Validación completada: {analizados}, {informativas}, {omitibles}.{advertencias} Sin incidencias bloqueantes.";
    }

    /// <summary>
    /// Formatea un conteo con la concordancia singular/plural correspondiente.
    /// </summary>
    private static string Conteo(int cantidad, string singular, string plural) =>
        $"{cantidad:N0} {(cantidad == 1 ? singular : plural)}";

    private static string FormatearNumero(long valor) => valor.ToString("N0", CultureInfo.CurrentCulture);

    private static string FormatearDuracion(TimeSpan duracion) => duracion.TotalSeconds >= 1
        ? $"{duracion.TotalSeconds:0.00} s"
        : $"{duracion.TotalMilliseconds:0} ms";

    private static string ComponerTextoGeometria(ShapefileInfoDto info)
    {
        var declarado = info.DeclaredShapeType ?? "No disponible";

        return info.ObservedGeometryTypes.Count == 0
            ? declarado
            : $"{declarado} | leídos: {string.Join(", ", info.ObservedGeometryTypes)}";
    }

    private static string ComponerTextoReferencia(SpatialReferenceDto referencia)
    {
        if (!referencia.IsAvailable)
        {
            return "No verificable (falta el archivo .prj)";
        }

        var tipo = referencia.IsGeographic ? "geográfico" : "proyectado";
        var srid = referencia.Srid.HasValue ? $"EPSG:{referencia.Srid.Value}" : "EPSG no determinado";
        var nombre = string.IsNullOrWhiteSpace(referencia.Name) ? "sin nombre declarado" : referencia.Name;

        return referencia.IsWgs84
            ? $"{srid} - {nombre} ({tipo})"
            : $"{srid} - {nombre} ({tipo}) [no corresponde a WGS 84]";
    }

    private static string ComponerTextoReferenciaCorto(SpatialReferenceDto referencia)
    {
        if (!referencia.IsAvailable)
        {
            return "No verificable";
        }

        return referencia.Srid.HasValue ? $"EPSG:{referencia.Srid.Value}" : "EPSG sin determinar";
    }

    private static string ComponerDetalleReferencia(SpatialReferenceDto referencia)
    {
        var partes = new List<string>(3);

        if (!referencia.IsAvailable)
        {
            partes.Add(referencia.Note ?? "No se encontró el archivo .prj.");
            return string.Join(Environment.NewLine + Environment.NewLine, partes);
        }

        if (referencia.DetectionEvidence is { Length: > 0 } evidencia)
        {
            partes.Add(evidencia);
        }

        if (referencia.Note is { Length: > 0 } observacion)
        {
            partes.Add(observacion);
        }

        if (referencia.Wkt is { Length: > 0 } wkt)
        {
            var wktRecortado = wkt.Trim();
            partes.Add("WKT: " + (wktRecortado.Length > 300 ? wktRecortado[..300] + "..." : wktRecortado));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, partes);
    }

    /// <summary>
    /// Muestra en la tarjeta de estado el resultado de la inspección (Fase 1).
    /// </summary>
    private void MostrarEstadoInspeccion()
    {
        if (_inspeccionActual is null)
        {
            _cardEstado.Set("Estado", TextoSinValor, ColorTexto);
            return;
        }

        _cardEstado.Set("Estado", _inspeccionActual.Status.ToDisplayName(), ColorParaEstado(_inspeccionActual.Status));
    }

    /// <summary>
    /// Indica si la validación registró incidencias bloqueantes (errores u omisiones).
    /// </summary>
    private static bool TieneIncidenciasBloqueantes(ShapefileValidationResultDto resultado) =>
        resultado.Status == ShapefileInspectionStatus.Failed
        || resultado.Summary.OmitableRecords > 0
        || resultado.Summary.ErrorIssueCount > 0;

    /// <summary>
    /// Texto de la tarjeta de estado a partir del resultado de la validación (Fase 2).
    /// </summary>
    private static string ComponerEstadoValidacion(ShapefileValidationResultDto resultado) =>
        TieneIncidenciasBloqueantes(resultado)
            ? "Con incidencias"
            : resultado.Summary.WarningIssueCount > 0
                ? "Con advertencias"
                : "Correcto";

    /// <summary>
    /// Color de la tarjeta de estado a partir del resultado de la validación (Fase 2).
    /// </summary>
    private static Color ColorParaEstadoValidacion(ShapefileValidationResultDto resultado) =>
        TieneIncidenciasBloqueantes(resultado)
            ? ColorError
            : resultado.Summary.WarningIssueCount > 0
                ? ColorAdvertencia
                : ColorOk;

    private static Color ColorParaEstado(ShapefileInspectionStatus estado) => estado switch
    {
        ShapefileInspectionStatus.Success => ColorOk,
        ShapefileInspectionStatus.Warning => ColorAdvertencia,
        _ => ColorError
    };

    private static Color ColorParaReferencia(SpatialReferenceDto referencia) =>
        referencia.IsAvailable && referencia.IsWgs84 ? ColorOk : ColorAdvertencia;

    private static Color ColorParaSeveridad(ValidationSeverity severidad) => severidad switch
    {
        ValidationSeverity.Info => ColorInfo,
        ValidationSeverity.Warning => ColorAdvertencia,
        _ => ColorError
    };

    private static Color ColorSuaveParaSeveridad(ValidationSeverity severidad) => severidad switch
    {
        ValidationSeverity.Info => ColorSuaveInfo,
        ValidationSeverity.Warning => ColorSuaveAdvertencia,
        _ => ColorSuaveError
    };

    private static Color ColorParaDisposicion(RecordDisposition disposicion) => disposicion switch
    {
        RecordDisposition.Accept => ColorOk,
        RecordDisposition.AcceptWithWarning => ColorAdvertencia,
        _ => ColorError
    };
}
