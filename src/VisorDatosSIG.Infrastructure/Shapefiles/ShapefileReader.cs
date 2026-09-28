using System.Globalization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Dbf.Fields;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;
using EsriShapefileReader = NetTopologySuite.IO.Esri.Shapefiles.Readers.ShapefileReader;
using EsriShapefileReaderOptions = NetTopologySuite.IO.Esri.Shapefiles.Readers.ShapefileReaderOptions;
using EsriGeometryBuilderMode = NetTopologySuite.IO.Esri.Shapefiles.Readers.GeometryBuilderMode;

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
                var readerOptions = new EsriShapefileReaderOptions
                {
                    // Los registros se leen tal como están en el archivo: en esta fase no se
                    // reparan ni se descartan geometrías inválidas (eso corresponde a la
                    // validación detallada de la siguiente fase).
                    GeometryBuilderMode = EsriGeometryBuilderMode.IgnoreInvalidShapes
                };

                using var reader = Shapefile.OpenRead(fullPath, readerOptions);

                recordCount = reader.RecordCount;
                fields = MapFields(reader.Fields);
                dbfEncodingName = reader.Encoding?.WebName;
                extentText = FormatExtent(reader.BoundingBox);
                extentWithinGeographicRange = IsWithinGeographicRange(reader.BoundingBox);
                shapeType = Shapefile.GetShapeType(fullPath);
                declaredShapeType = DescribeShapeType(shapeType);

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
                errors.Add($"Ocurrió un error al leer el archivo '{fileName}': {DescribeException(ex)}");
            }
        }

        if (errors.Count == 0 && layerDetection.IsRecognized
            && !MatchesExpectedGeometry(layerDetection.Layer, shapeType))
        {
            warnings.Add($"El tipo de geometría declarado ({declaredShapeType}) no coincide con el esperado para la capa " +
                         $"{layerDetection.LayerDisplayName} ({ExpectedGeometryDescription(layerDetection.Layer)}).");
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
            warnings.Add($"No se pudo leer el archivo .prj: {DescribeException(ex)}");
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
    /// Convierte las definiciones de campos del DBF en el DTO que utiliza la aplicación.
    /// </summary>
    private static IReadOnlyList<ShapefileFieldDto> MapFields(DbfFieldCollection dbfFields)
    {
        var fields = new List<ShapefileFieldDto>(dbfFields.Count);
        for (var index = 0; index < dbfFields.Count; index++)
        {
            var field = dbfFields[index];
            fields.Add(new ShapefileFieldDto
            {
                Index = index,
                Name = field.Name,
                DataType = field.FieldType.ToString(),
                Length = field.Length,
                DecimalCount = field.NumericScale
            });
        }

        return fields;
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

            var values = new Dictionary<string, string?>(fields.Count, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < fields.Count; index++)
            {
                var value = index < reader.Fields.Count ? reader.Fields[index].Value : null;
                values[fields[index].Name] = FormatAttributeValue(value);
            }

            records.Add(new ShapefileRecordDto
            {
                RecordNumber = recordNumber,
                GeometryType = geometryType,
                Values = values
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
    /// Convierte el valor de un campo del DBF a texto sin modificar su contenido
    /// (no se recortan espacios porque esta fase no debe corregir valores silenciosamente).
    /// </summary>
    private static string? FormatAttributeValue(object? value) => value switch
    {
        null => null,
        string text => text,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        double number => number.ToString("0.############", CultureInfo.InvariantCulture),
        float number => number.ToString("0.############", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.############", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    /// <summary>
    /// Da formato a la extensión (bounding box) declarada en el encabezado del SHP.
    /// </summary>
    private static string? FormatExtent(Envelope? envelope)
    {
        if (envelope is null || envelope.IsNull)
        {
            return null;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "X: {0:0.######} a {1:0.######} | Y: {2:0.######} a {3:0.######}",
            envelope.MinX,
            envelope.MaxX,
            envelope.MinY,
            envelope.MaxY);
    }

    /// <summary>
    /// Verifica si la extensión está dentro del rango válido de coordenadas geográficas.
    /// Sirve como evidencia adicional para justificar un sistema geográfico.
    /// </summary>
    private static bool? IsWithinGeographicRange(Envelope? envelope)
    {
        if (envelope is null || envelope.IsNull)
        {
            return null;
        }

        return envelope.MinX >= -180 && envelope.MaxX <= 180
               && envelope.MinY >= -90 && envelope.MaxY <= 90;
    }

    /// <summary>
    /// Describe el tipo de geometría declarado en el encabezado del archivo SHP.
    /// </summary>
    private static string DescribeShapeType(ShapeType shapeType)
    {
        var kind = shapeType switch
        {
            ShapeType.NullShape => "nulo",
            _ when shapeType.IsPoint() => "punto",
            _ when shapeType.IsMultiPoint() => "multipunto",
            _ when shapeType.IsPolyLine() => "línea",
            _ when shapeType.IsPolygon() => "polígono",
            _ => "desconocido"
        };

        var dimensions = new List<string>(2);
        if (shapeType.HasZ())
        {
            dimensions.Add("con Z");
        }

        if (shapeType.HasM())
        {
            dimensions.Add("con M");
        }

        var suffix = dimensions.Count > 0 ? $", {string.Join(", ", dimensions)}" : string.Empty;
        return $"{shapeType} ({kind}{suffix})";
    }

    /// <summary>
    /// Compara el tipo de geometría del archivo con el esperado para la capa detectada.
    /// </summary>
    private static bool MatchesExpectedGeometry(ShapefileLayer layer, ShapeType shapeType) => layer switch
    {
        ShapefileLayer.Manzanas or ShapefileLayer.Lotes => shapeType.IsPolygon(),
        ShapefileLayer.CodigosFijos => shapeType.IsPoint(),
        ShapefileLayer.Vias => shapeType.IsPolyLine(),
        _ => true
    };

    /// <summary>
    /// Describe la geometría esperada para la capa detectada.
    /// </summary>
    private static string ExpectedGeometryDescription(ShapefileLayer layer) => layer switch
    {
        ShapefileLayer.Manzanas or ShapefileLayer.Lotes => "polígono / multipolígono",
        ShapefileLayer.CodigosFijos => "punto",
        ShapefileLayer.Vias => "línea / multilínea",
        _ => "sin restricción"
    };

    /// <summary>
    /// Describe una excepción técnica de forma breve y comprensible.
    /// </summary>
    private static string DescribeException(Exception exception) =>
        $"{exception.GetType().Name}: {exception.Message}";

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
