namespace VisorDatosSIG.Domain.Entities.Cadastre;

public sealed class CodigoFijo
{
    public int IdCodigo { get; set; }
    public int? CodF_SQL { get; set; }
    public string? CodF_SIG { get; set; }
    public int? CodFijo { get; set; }
    public string? Nombre { get; set; }
    public byte Estado { get; set; } = 1;
    public DateTime FechaCambioEstado { get; set; } = DateTime.UtcNow;
    public int? IdLote { get; set; }
    public double? Longitud { get; set; }
    public double? Latitud { get; set; }
}
