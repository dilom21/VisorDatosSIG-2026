using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Api.Middleware;

/// <summary>
/// Middleware que captura las excepciones no controladas y devuelve una respuesta 500
/// sin detalles técnicos ni trazas.
/// </summary>
/// <remarks>
/// El detalle de la excepción se escribe únicamente en el log del servidor.
/// Nunca se registra el cuerpo de la petición (puede contener contraseñas).
/// </remarks>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ExceptionHandlingMiddleware> _registro;

    /// <summary>
    /// Inicializa el middleware.
    /// </summary>
    public ExceptionHandlingMiddleware(RequestDelegate siguiente, ILogger<ExceptionHandlingMiddleware> registro)
    {
        _siguiente = siguiente;
        _registro = registro;
    }

    /// <summary>
    /// Ejecuta el middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _siguiente(contexto);
        }
        catch (Exception excepcion)
        {
            _registro.LogError(
                excepcion,
                "Error no controlado al procesar {Metodo} {Ruta}",
                contexto.Request.Method,
                contexto.Request.Path);

            if (contexto.Response.HasStarted)
            {
                throw;
            }

            contexto.Response.Clear();
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            contexto.Response.ContentType = "application/problem+json";

            await contexto.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                Detail = "Ocurrió un error inesperado al procesar la solicitud."
            });
        }
    }
}
