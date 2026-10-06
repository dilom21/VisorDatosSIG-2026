namespace VisorDatosSIG.Application.Exceptions;

/// <summary>
/// Indicates that a partial-leave period conflicts with another active period.
/// </summary>
public sealed class BajaParcialSolapamientoException : InvalidOperationException
{
    public BajaParcialSolapamientoException()
        : base("El empleado ya posee una baja parcial que coincide con el periodo indicado.")
    {
    }
}
