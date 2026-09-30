namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Definición de un campo (columna) del archivo DBF asociado al shapefile.
/// </summary>
public sealed class ShapefileFieldDto
{
    /// <summary>Posición del campo dentro del DBF (base cero).</summary>
    public required int Index { get; init; }

    /// <summary>Nombre del campo tal como aparece en el DBF.</summary>
    public required string Name { get; init; }

    /// <summary>Tipo de dato dBASE del campo (Character, Numeric, Date, Logical, Float, Shape).</summary>
    public required string DataType { get; init; }

    /// <summary>Longitud del campo en bytes o cantidad total de dígitos.</summary>
    public required int Length { get; init; }

    /// <summary>Cantidad de decimales declarados para el campo.</summary>
    public required int DecimalCount { get; init; }

    /// <summary>Descripción legible del tipo de dato.</summary>
    public string DisplayType => DecimalCount > 0
        ? $"{DataType} ({Length},{DecimalCount})"
        : $"{DataType} ({Length})";
}
