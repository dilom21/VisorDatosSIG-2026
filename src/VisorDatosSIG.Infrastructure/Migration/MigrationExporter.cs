using System.Globalization;
using System.Text;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Migration;

/// <summary>
/// Exportador de informes y evidencias técnicas de la migración (RF-MIG-13).
/// </summary>
public sealed class MigrationExporter : IMigrationExporter
{
    public async Task ExportarTextoAsync(
        string rutaArchivo,
        ShapefileInfoDto? inspeccion,
        ShapefileValidationResultDto? validacion,
        MigrationResult? migracion,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine(" UNIVERSIDAD AUTÓNOMA GABRIEL RENÉ MORENO - FICCT");
        sb.AppendLine(" SISTEMAS DE INFORMACIÓN GEOGRÁFICA (SIG 2026)");
        sb.AppendLine(" INFORME TÉCNICO DE MIGRACIÓN DE DATOS ESPACIALES (VisorDatosSIG)");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Fecha y hora de generación : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Equipo / Máquina           : {Environment.MachineName}");
        sb.AppendLine($"Usuario del sistema        : {Environment.UserName}");
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("1. FICHA TÉCNICA DEL ARCHIVO SHAPEFILE");
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (inspeccion is not null)
        {
            sb.AppendLine($"Archivo origen       : {inspeccion.FileName}");
            sb.AppendLine($"Ruta completa        : {inspeccion.FilePath}");
            sb.AppendLine($"Capa oficial         : {inspeccion.LayerDisplayName} ({inspeccion.Layer})");
            sb.AppendLine($"Total registros DBF  : {inspeccion.RecordCount:N0}");
            sb.AppendLine($"Tipo de geometría    : {inspeccion.DeclaredShapeType ?? "No especificado"}");
            sb.AppendLine($"Sistema de referencia: EPSG:{inspeccion.SpatialReference.Srid} ({inspeccion.SpatialReference.Name})");
            sb.AppendLine($"WGS 84 válido        : {(inspeccion.SpatialReference.IsWgs84 ? "SÍ" : "NO")}");
            sb.AppendLine($"Extensión (BBox)     : {inspeccion.ExtentText ?? "No disponible"}");
            sb.AppendLine($"Codificación DBF     : {inspeccion.DbfEncodingName ?? "No disponible"}");

            sb.AppendLine();
            sb.AppendLine("Componentes del Shapefile:");
            foreach (var comp in inspeccion.Components)
            {
                var estado = comp.Exists ? "PRESENTE" : (comp.IsRequired ? "FALTANTE (OBLIGATORIO)" : "FALTANTE (OPCIONAL)");
                sb.AppendLine($"  - .{comp.Type.ToString().ToLowerInvariant()}: {estado} ({comp.FilePath})");
            }

            sb.AppendLine();
            sb.AppendLine("Campos alfanuméricos (DBF):");
            foreach (var f in inspeccion.Fields)
            {
                sb.AppendLine($"  - {f.Name,-15} Tipo: {f.DataType,-10} Longitud: {f.Length,-5} Decimales: {f.DecimalCount}");
            }
        }
        else
        {
            sb.AppendLine("No se proporcionó información de inspección.");
        }

        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("2. RESULTADOS DE LA VALIDACIÓN PREVIA DE INTEGRIDAD");
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (validacion is not null)
        {
            sb.AppendLine($"Registros analizados : {validacion.Summary.AnalyzedRecords:N0}");
            sb.AppendLine($"Registros válidos    : {validacion.Summary.ValidRecords:N0}");
            sb.AppendLine($"Registros c/ avisos  : {validacion.Summary.RecordsWithWarnings:N0}");
            sb.AppendLine($"Registros omitibles  : {validacion.Summary.OmitableRecords:N0}");
            sb.AppendLine($"Total incidencias    : {validacion.Summary.TotalIssueCount:N0} (Errores: {validacion.Summary.ErrorIssueCount}, Advertencias: {validacion.Summary.WarningIssueCount}, Info: {validacion.Summary.InfoIssueCount})");
            sb.AppendLine($"Estado de validación : {validacion.Status.ToDisplayName()}");
        }
        else
        {
            sb.AppendLine("No se ejecutó la fase de validación detallada.");
        }

        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("3. RESULTADOS DE LA MIGRACIÓN EN SQL SERVER");
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (migracion is not null)
        {
            sb.AppendLine($"Estado de la carga   : {migracion.Status}");
            sb.AppendLine($"Modalidad aplicada   : {(migracion.Mode == MigrationMode.Replace ? "REEMPLAZAR (Borrado previo seguro)" : "ANEXAR SIN DUPLICAR (Append)")}");
            sb.AppendLine($"Tabla de destino     : {migracion.DestinationTable}");
            sb.AppendLine($"Registros leídos     : {migracion.ProcessedRecords:N0} de {migracion.SourceRecords:N0}");
            sb.AppendLine($"Registros insertados : {migracion.InsertedRecords:N0}");
            sb.AppendLine($"Registros omitidos   : {migracion.OmittedRecords:N0}");
            sb.AppendLine($"Registros fallidos   : {migracion.FailedRecords:N0}");
            sb.AppendLine($"Duración total       : {migracion.Duration.TotalSeconds:F2} segundos ({migracion.Duration})");

            if (migracion.Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Advertencias registradas durante la migración:");
                foreach (var w in migracion.Warnings)
                {
                    sb.AppendLine($"  * [AVISO] {w}");
                }
            }

            if (migracion.Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Errores registrados durante la migración:");
                foreach (var err in migracion.Errors)
                {
                    sb.AppendLine($"  ! [ERROR] {err}");
                }
            }
        }
        else
        {
            sb.AppendLine("No se ha ejecutado la migración para esta capa.");
        }

        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine(" Fin del informe técnico");
        sb.AppendLine("================================================================================");

        await File.WriteAllTextAsync(rutaArchivo, sb.ToString(), Encoding.UTF8, cancellationToken);
    }

    public async Task ExportarCsvAsync(
        string rutaArchivo,
        ShapefileInfoDto? inspeccion,
        ShapefileValidationResultDto? validacion,
        MigrationResult? migracion,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fila;Severidad;TipoIncidencia;Campo;Valor;Mensaje;Accion");

        if (validacion?.Issues is not null)
        {
            foreach (var issue in validacion.Issues)
            {
                var fila = issue.RecordNumber > 0 ? issue.RecordNumber.ToString(CultureInfo.InvariantCulture) : "N/A";
                var severidad = EscapeCsv(issue.Severity.ToString());
                var tipo = EscapeCsv(issue.IssueType.ToString());
                var campo = EscapeCsv(issue.Field ?? string.Empty);
                var valor = EscapeCsv(issue.Value ?? string.Empty);
                var mensaje = EscapeCsv(issue.Message);
                var accion = EscapeCsv(issue.Disposition.ToString());

                sb.AppendLine($"{fila};{severidad};{tipo};{campo};{valor};{mensaje};{accion}");
            }
        }

        await File.WriteAllTextAsync(rutaArchivo, sb.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static string EscapeCsv(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        if (texto.Contains(';') || texto.Contains('"') || texto.Contains('\n') || texto.Contains('\r'))
        {
            return $"\"{texto.Replace("\"", "\"\"")}\"";
        }

        return texto;
    }
}
