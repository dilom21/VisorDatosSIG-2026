using NetTopologySuite.IO.Esri;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;
using EsriShapefileReader = NetTopologySuite.IO.Esri.Shapefiles.Readers.ShapefileReader;

namespace VisorDatosSIG.Infrastructure.Shapefiles;

/// <summary>
/// Inspección de shapefiles en modo solo lectura mediante NetTopologySuite.IO.Esri.
/// </summary>
/// <remarks>
/// Fase 1 del Migrador: valida los archivos asociados, detecta la capa, lee la estructura
/// y previsualiza los primeros registros. No escribe archivos, no repara geometrías,
/// no transforma coordenadas y no accede a SQL Server.
/// </remarks>
public sealed class ShapefileReader : IShapefileReader
{
    /// <summary>Cantidad máxima de registros que se previsualizan.</summary>
    public const int MaxPreviewRecords = 20;

    private static readonly ShapefileComponentDefinition[] ComponentDefinitions =
    [
        new(ShapefileComponentType.Shp, ".shp", "Geometrías", true),
        new(ShapefileComponentType.Shx, ".shx", "Índice espacial", true),
        new(ShapefileComponentType.Dbf, ".dbf", "Atributos (DBF)", true),
        new(ShapefileComponentType.Prj, ".prj", "Referencia espacial (WKT)", false)
    ];

    private readonly IShapefileLayerDetector _layerDetector;

    /// <summary>
    /// Inicializa el lector con el detector centralizado de capas oficiales.
    /// </summary>
    /// <param name="layerDetector">Detector centralizado de capas.</param>
    public ShapefileReader(IShapefileLayerDetector layerDetector)
    {
        _layerDetector = layerDetector ?? throw new ArgumentNullException(nameof(layerDetector));
    }

    /// <inheritdoc />
    public Task<ShapefileInfoDto> InspectAsync(string shpPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shpPath))
        {
            throw new ArgumentException("La ruta del archivo shapefile no puede estar vacía.", nameof(shpPath));
        }

        // La lectura de archivos es una operación de E/S bloqueante: se ejecuta en un hilo
        // de trabajo para no bloquear la interfaz del Migrador.
        return Task.Run(() => Inspect(shpPath, cancellationToken), cancellationToken);
    }

    private ShapefileInfoDto Inspect(string shpPath, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var fullPath = Path.GetFullPath(shpPath);
        var fileName = Path.GetFileName(fullPath);

        var layerDetection = _layerDetector.Detect(fileName);
        if (!layerDetection.IsRecognized)
        {
            warnings.Add($"Capa no reconocida: el archivo '{fileName}' no corresponde a ninguna de las capas oficiales " +
                         $"(Manzanas, Lotes, Códigos Fijos o Vías). {layerDetection.AppliedRule}");
        }
        else if (layerDetection.IsAmbiguous)
        {
            warnings.Add($"El nombre del archivo coincide con más de una capa oficial " +
                         $"({string.Join(", ", layerDetection.MatchedLayers.Select(layer => layer.ToDisplayName()))}); " +
                         $"se aplicó la capa {layerDetection.LayerDisplayName}.");
        }

        var components = BuildComponents(fullPath);

        if (!string.Equals(Path.GetExtension(fullPath), ".shp", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"El archivo seleccionado debe tener la extensión .shp (se recibió '{Path.GetExtension(fullPath)}').");
        }

        if (!File.Exists(fullPath))
        {
            errors.Add($"El archivo '{fileName}' no existe en la ruta indicada.");
        }

        foreach (var component in components.Where(component => component.IsRequired && !component.Exists))
        {
            errors.Add($"Falta el componente obligatorio '{component.Extension}' ({component.Description}) del shapefile " +
                       $"'{fileName}': no es posible continuar con la lectura.");
        }

        var prjComponent = components.Single(component => component.Type == ShapefileComponentType.Prj);
        if (!prjComponent.Exists)
        {
            warnings.Add("No se encontró el archivo .prj: no es posible verificar la referencia espacial del shapefile " +
                         "(las capas oficiales se diagnostican en WGS 84 / EPSG:4326).");
        }

        var spatialReference = ReadSpatialReference(prjComponent, warnings);

        var recordCount = 0;
        var shapeType = ShapeType.NullShape;
        string? declaredShapeType = null;
        string? dbfEncodingName = null;
        string? extentText = null;
        bool? extentWithinGeographicRange = null;
        var observedGeometryTypes = new List<string>();
        IReadOnlyList<ShapefileFieldDto> fields = Array.Empty<ShapefileFieldDto>();
        var preview = ShapefilePreviewDto.CreateEmpty(MaxPreviewRecords);

        if (errors.Count == 0)
        {
            try
            {
                using var reader = ShapefileAccess.OpenReader(fullPath);

                recordCount = reader.RecordCount;
                fields = ShapefileAccess.MapFields(reader.Fields);
                dbfEncodingName = reader.Encoding?.WebName;
                extentText = ShapefileAccess.FormatExtent(reader.BoundingBox);
                extentWithinGeographicRange = ShapefileAccess.IsWithinGeographicRange(reader.BoundingBox);
                shapeType = Shapefile.GetShapeType(fullPath);
                declaredShapeType = ShapefileAccess.DescribeShapeType(shapeType);

                preview = ReadPreview(reader, fields, observedGeometryTypes, cancellationToken);

                if (recordCount == 0)
                {
                    warnings.Add("El archivo no contiene registros.");
                }
                else if (recordCount <= MaxPreviewRecords && preview.Records.Count != recordCount)
                {
                    warnings.Add($"El archivo DBF declara {recordCount} registro(s) y solo se pudieron leer " +
                                 $"{preview.Records.Count} para la previsualización.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"Ocurrió un error al leer el archivo '{fileName}': {ShapefileAccess.DescribeException(ex)}");
            }
        }

        if (errors.Count == 0 && layerDetection.IsRecognized
            && !ShapefileAccess.MatchesExpectedShapeType(layerDetection.Layer, shapeType))
        {
            warnings.Add($"El tipo de geometría declarado ({declaredShapeType}) no coincide con el esperado para la capa " +
                         $"{layerDetection.LayerDisplayName} ({ShapefileAccess.ExpectedGeometryDescription(layerDetection.Layer)}).");
        }

        var status = errors.Count > 0
            ? ShapefileInspectionStatus.Failed
            : warnings.Count > 0
                ? ShapefileInspectionStatus.Warning
                : ShapefileInspectionStatus.Success;

        var statusMessage = status switch
        {
            ShapefileInspectionStatus.Failed => "No se pudo inspeccionar el archivo",
            _ when !layerDetection.IsRecognized => "Capa no reconocida",
            ShapefileInspectionStatus.Warning => "Inspección completada con advertencias",
            _ => "Inspección completada"
        };

        return new ShapefileInfoDto
        {
            FilePath = fullPath,
            FileName = fileName,
            Layer = layerDetection.Layer,
            LayerDisplayName = layerDetection.LayerDisplayName,
            Status = status,
            StatusMessage = statusMessage,
            Components = components,
            Fields = fields,
            Preview = preview,
            SpatialReference = spatialReference,
            Errors = errors,
            Warnings = warnings,
            RecordCount = recordCount,
            DeclaredShapeType = declaredShapeType,
            ObservedGeometryTypes = observedGeometryTypes,
            ExtentText = extentText,
            ExtentWithinGeographicRange = extentWithinGeographicRange,
            DbfEncodingName = dbfEncodingName
        };
    }

    /// <summary>
    /// Verifica la existencia de los archivos asociados del mismo nombre y extensión.
    /// </summary>
    private static IReadOnlyList<ShapefileComponentDto> BuildComponents(string shpPath)
    {
        var directory = Path.GetDirectoryName(shpPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(shpPath);

        return ComponentDefinitions
            .Select(definition =>
            {
                var expectedPath = Path.Combine(directory, baseName + definition.Extension);
                return new ShapefileComponentDto
                {
                    Type = definition.Type,
                    Extension = definition.Extension,
                    Description = definition.Description,
                    IsRequired = definition.IsRequired,
                    Exists = File.Exists(expectedPath),
                    FilePath = expectedPath
                };
            })
            .ToArray();
    }

    /// <summary>
    /// Lee e interpreta la referencia espacial del archivo .prj.
    /// </summary>
    private static SpatialReferenceDto ReadSpatialReference(ShapefileComponentDto prjComponent, List<string> warnings)
    {
        if (!prjComponent.Exists)
        {
            return PrjSpatialReferenceReader.Parse(null);
        }

        string wkt;
        try
        {
            wkt = File.ReadAllText(prjComponent.FilePath);
        }
        catch (Exception ex)
        {
            warnings.Add($"No se pudo leer el archivo .prj: {ShapefileAccess.DescribeException(ex)}");
            return PrjSpatialReferenceReader.Parse(null);
        }

        var spatialReference = PrjSpatialReferenceReader.Parse(wkt);
        if (spatialReference.Note is not null)
        {
            warnings.Add(spatialReference.Note);
        }

        return spatialReference;
    }

    /// <summary>
    /// Lee como máximo <see cref="MaxPreviewRecords"/> registros con su geometría y sus atributos.
    /// </summary>
    private static ShapefilePreviewDto ReadPreview(
        EsriShapefileReader reader,
        IReadOnlyList<ShapefileFieldDto> fields,
        List<string> observedGeometryTypes,
        CancellationToken cancellationToken)
    {
        var records = new List<ShapefileRecordDto>(MaxPreviewRecords);
        var buffer = new Dictionary<string, string?>(fields.Count, StringComparer.OrdinalIgnoreCase);
        long recordNumber = 0;

        // Read() avanza al siguiente registro y omite los marcados como eliminados en el DBF.
        while (records.Count < MaxPreviewRecords && reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordNumber++;

            var geometry = reader.Geometry;
            var geometryType = geometry is null || geometry.IsEmpty ? "Sin geometría" : geometry.GeometryType;
            if (!observedGeometryTypes.Contains(geometryType, StringComparer.Ordinal))
            {
                observedGeometryTypes.Add(geometryType);
            }

            // El búfer se reutiliza entre registros: cada fila conserva su propia copia.
            ShapefileAccess.FillRecordValues(reader, fields, buffer);

            records.Add(new ShapefileRecordDto
            {
                RecordNumber = recordNumber,
                GeometryType = geometryType,
                Values = new Dictionary<string, string?>(buffer, StringComparer.OrdinalIgnoreCase)
            });
        }

        return new ShapefilePreviewDto
        {
            ColumnNames = fields.Select(field => field.Name).ToArray(),
            Records = records,
            MaxRecords = MaxPreviewRecords
        };
    }

    /// <summary>
    /// Definición de un archivo asociado al shapefile.
    /// </summary>
    /// <param name="Type">Tipo de componente.</param>
    /// <param name="Extension">Extensión del archivo.</param>
    /// <param name="Description">Descripción funcional.</param>
    /// <param name="IsRequired">Indica si es obligatorio para leer el shapefile.</param>
    private sealed record ShapefileComponentDefinition(
        ShapefileComponentType Type,
        string Extension,
        string Description,
        bool IsRequired);
}
