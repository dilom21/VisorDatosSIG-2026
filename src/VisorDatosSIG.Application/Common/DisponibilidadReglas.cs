using VisorDatosSIG.Application.DTOs.Disponibilidad;

namespace VisorDatosSIG.Application.Common;

public static class DisponibilidadReglas
{
    public static string? ValidarHorario(byte diaSemana, TimeOnly inicio, TimeOnly fin)
    {
        if (diaSemana is < 1 or > 7) return "El día de la semana debe estar entre 1 y 7.";
        if (inicio >= fin) return "La hora de inicio debe ser anterior a la hora de fin.";
        return null;
    }

    public static bool SeSolapan(TimeOnly inicio, TimeOnly fin, TimeOnly existenteInicio, TimeOnly existenteFin) =>
        inicio < existenteFin && fin > existenteInicio;

    public static bool SeSolapan(DateTime inicio, DateTime fin, DateTime existenteInicio, DateTime existenteFin) =>
        inicio < existenteFin && fin > existenteInicio;

    public static int DiaIso(DayOfWeek dia) => dia == DayOfWeek.Sunday ? 7 : (int)dia;

    public static string? ValidarAsignacion(GuardarAsignacionTrabajoDto dto)
    {
        if (dto.FechaInicio >= dto.FechaFin) return "La fecha de inicio debe ser anterior a la fecha de fin.";
        if (!CatalogosDisponibilidad.TiposTarea.Contains(dto.TipoTarea.Trim())) return "El tipo de tarea no es válido.";
        if (!CatalogosDisponibilidad.EstadosAsignacion.Contains(dto.Estado.Trim())) return "El estado de la asignación no es válido.";
        if (!CatalogosDisponibilidad.Prioridades.Contains(dto.Prioridad.Trim())) return "La prioridad no es válida.";
        return null;
    }

    public static string CalcularEstado(bool activo, bool bajaVigente, bool enHorario, bool asignacionOcupante) =>
        !activo ? "Inactivo"
        : bajaVigente ? "Baja Parcial"
        : !enHorario ? "Fuera de horario"
        : asignacionOcupante ? "En Servicio"
        : "Disponible";
}
