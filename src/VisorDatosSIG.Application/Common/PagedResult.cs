namespace VisorDatosSIG.Application.Common;

/// <summary>
/// Envoltorio genérico para respuestas paginadas de la API (RF-CON-06).
/// </summary>
public sealed record PagedResult<T>
{
    public int Pagina { get; init; } = 1;
    public int Limite { get; init; } = 20;
    public int TotalRegistros { get; init; }
    public int TotalPaginas => Limite <= 0 ? 0 : (int)Math.Ceiling(TotalRegistros / (double)Limite);
    public IReadOnlyList<T> Datos { get; init; } = Array.Empty<T>();
}
