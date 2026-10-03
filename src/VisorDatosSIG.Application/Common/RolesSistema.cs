using System.Globalization;
using System.Text;

namespace VisorDatosSIG.Application.Common;

/// <summary>
/// Reglas de negocio asociadas a los roles del sistema VisorDatosSIG.
/// </summary>
/// <remarks>
/// Los roles se identifican por <c>NombreRol</c> normalizado (sin distinción de
/// mayúsculas, sin acentos y con espacios colapsados), nunca por <c>IdRol</c>: los
/// identificadores son <c>IDENTITY</c> y pueden variar entre bases de datos.
/// <para>
/// <b>Migrador:</b> el rol del migrador no participa de la seguridad web. Se excluye de
/// la administración de roles y de la resolución de permisos del VisorDatosSIG.Web; el
/// inicio de sesión y las operaciones del Migrador siguen funcionando sin cambios.
/// </para>
/// <para>
/// <b>Administrador:</b> es el rol del sistema y tiene acceso total implícito (no depende
/// de filas en <c>dbo.RolMenu</c>). Solo coincide el nombre exacto normalizado, de modo que
/// un rol parecido (por ejemplo "Administrador General") no hereda ese acceso; además, esos
/// nombres parecidos se rechazan al crear o renombrar un rol.
/// </para>
/// </remarks>
public static class RolesSistema
{
    /// <summary>Nombre del rol del sistema con acceso total implícito.</summary>
    public const string Administrador = "Administrador";

    /// <summary>Nombre del rol de consulta de la versión web.</summary>
    public const string Consultor = "Consultor";

    /// <summary>Nombre del rol de supervisión operativa y servicios (CU04).</summary>
    public const string Supervisor = "Supervisor";

    /// <summary>Nombre del rol responsable de la migración de datos geográficos.</summary>
    public const string ResponsableMigrador = "RESPONSABLE MIGRADOR";

    /// <summary>Nombre histórico que también identifica al rol del migrador.</summary>
    public const string Migrador = "MIGRADOR";

    /// <summary>Longitud máxima admitida por <c>dbo.Roles.NombreRol</c>.</summary>
    public const int LongitudMaximaNombre = 50;

    /// <summary>Longitud máxima admitida por <c>dbo.Roles.Descripcion</c>.</summary>
    public const int LongitudMaximaDescripcion = 200;

    /// <summary>
    /// Normaliza un nombre de rol: recorta extremos, colapsa espacios internos, elimina
    /// acentos y pasa a mayúsculas invariantes.
    /// </summary>
    /// <param name="nombre">Nombre a normalizar.</param>
    /// <returns>Nombre normalizado; cadena vacía cuando el valor es nulo o vacío.</returns>
    public static string Normalizar(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return string.Empty;
        }

        var descompuesto = nombre.Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                constructor.Append(caracter);
            }
        }

        var sinAcentos = constructor.ToString().Normalize(NormalizationForm.FormC);
        var partes = sinAcentos.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', partes).ToUpperInvariant();
    }

    /// <summary>
    /// Indica si el nombre corresponde al rol Administrador del sistema.
    /// </summary>
    /// <param name="nombreRol">Nombre del rol tal como está almacenado.</param>
    public static bool EsAdministrador(string? nombreRol) =>
        Normalizar(nombreRol) == Normalizar(Administrador);

    /// <summary>
    /// Indica si el nombre corresponde al rol responsable de la migración (o a sus
    /// variantes históricas). Ese rol nunca participa de la seguridad web.
    /// </summary>
    /// <param name="nombreRol">Nombre del rol tal como está almacenado.</param>
    public static bool EsResponsableMigrador(string? nombreRol)
    {
        var normalizado = Normalizar(nombreRol);

        return normalizado == Normalizar(ResponsableMigrador)
            || normalizado == Normalizar(Migrador)
            || normalizado.StartsWith(Normalizar(ResponsableMigrador) + " ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Indica si el nombre está reservado por el sistema y, por lo tanto, no puede
    /// crear ni renombrar roles el módulo de seguridad web.
    /// </summary>
    /// <param name="nombreRol">Nombre propuesto para el rol.</param>
    /// <remarks>
    /// Reservados: el nombre exacto <c>Administrador</c> y cualquier variante que empiece
    /// por "Administrador " (evita roles ambiguos que parezcan del sistema) y el nombre del
    /// rol responsable de la migración con sus variantes.
    /// </remarks>
    public static bool EsNombreReservado(string? nombreRol)
    {
        var normalizado = Normalizar(nombreRol);

        if (normalizado.Length == 0)
        {
            return false;
        }

        return normalizado == Normalizar(Administrador)
            || normalizado.StartsWith(Normalizar(Administrador) + " ", StringComparison.Ordinal)
            || EsResponsableMigrador(normalizado);
    }

    /// <summary>
    /// Valida la longitud de un nombre de rol.
    /// </summary>
    /// <param name="nombreRol">Nombre propuesto.</param>
    /// <returns><c>true</c> cuando el nombre tiene contenido y no excede <see cref="LongitudMaximaNombre"/>.</returns>
    public static bool EsNombreValido(string? nombreRol) =>
        !string.IsNullOrWhiteSpace(nombreRol) && nombreRol.Trim().Length <= LongitudMaximaNombre;
}
