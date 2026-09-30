namespace VisorDatosSIG.Application.Common;

/// <summary>
/// Módulos oficiales del sistema VisorDatosSIG (según MODULOS SIG.txt).
/// </summary>
public static class ModulosSistema
{
    /// <summary>Módulo 1: Usuarios y Seguridad (CU01 al CU05)</summary>
    public const string UsuariosYSeguridad = "Usuarios y Seguridad";

    /// <summary>Módulo 2: Visor Cartográfico (CU06 al CU11)</summary>
    public const string VisorCartografico = "Visor Cartográfico";

    /// <summary>Módulo 3: Consultas y Filtros (CU12 al CU15)</summary>
    public const string ConsultasYFiltros = "Consultas y Filtros";

    /// <summary>Módulo 4: Migrador de Datos Geográficos (CU16 al CU21)</summary>
    public const string MigradorDeDatosGeograficos = "Migrador de Datos Geográficos";

    /// <summary>Módulo 5: Reportes (CU22 al CU25)</summary>
    public const string Reportes = "Reportes";

    /// <summary>
    /// Todos los módulos oficiales del sistema.
    /// </summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        UsuariosYSeguridad,
        VisorCartografico,
        ConsultasYFiltros,
        MigradorDeDatosGeograficos,
        Reportes
    ];

    /// <summary>
    /// Módulos consultables en la versión web (excluye el módulo de migración).
    /// </summary>
    public static readonly IReadOnlyList<string> ModulosWeb =
    [
        UsuariosYSeguridad,
        VisorCartografico,
        ConsultasYFiltros,
        Reportes
    ];
}
