using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Disponibilidad;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>Persistencia ADO.NET para horarios, disponibilidad efectiva y asignaciones (CU19).</summary>
public sealed class DisponibilidadRepository : IDisponibilidadRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    public DisponibilidadRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IReadOnlyList<HorarioTrabajoDto>?> ObtenerHorariosAsync(int idEmpleado, CancellationToken ct = default)
    {
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        if (!await ExisteEmpleadoAsync(cn, null, idEmpleado, false, ct)) return null;
        const string sql = """
            SELECT IdHorario, IdEmpleado, DiaSemana, HoraInicio, HoraFin, Activo, FechaRegistro, FechaModificacion
            FROM dbo.HorariosTrabajo WHERE IdEmpleado = @IdEmpleado
            ORDER BY DiaSemana, HoraInicio, IdHorario;
            """;
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@IdEmpleado", SqlDbType.Int).Value = idEmpleado;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<HorarioTrabajoDto>();
        while (await reader.ReadAsync(ct)) items.Add(MapearHorario(reader));
        return items;
    }

    public async Task<ResultadoOperacion<HorarioTrabajoDto>> CrearHorarioAsync(
        int idEmpleado, GuardarHorarioRequestDto dto, CancellationToken ct = default)
    {
        var error = DisponibilidadReglas.ValidarHorario(dto.DiaSemana, dto.HoraInicio, dto.HoraFin);
        if (error is not null) return ResultadoOperacion<HorarioTrabajoDto>.Invalido(error);
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (!await ExisteEmpleadoAsync(cn, tx, idEmpleado, false, ct))
                return await RevertirAsync(tx, ResultadoOperacion<HorarioTrabajoDto>.NoEncontrado("El empleado no existe."), ct);
            if (!await ExisteEmpleadoAsync(cn, tx, idEmpleado, true, ct))
                return await RevertirAsync(tx, ResultadoOperacion<HorarioTrabajoDto>.Conflicto("El empleado se encuentra inactivo."), ct);
            var conflicto = await ConflictoHorarioAsync(cn, tx, idEmpleado, dto, null, ct);
            if (conflicto is not null)
                return await RevertirAsync(tx, ResultadoOperacion<HorarioTrabajoDto>.Conflicto(conflicto), ct);
            const string sql = """
                INSERT dbo.HorariosTrabajo (IdEmpleado, DiaSemana, HoraInicio, HoraFin, Activo, FechaRegistro)
                OUTPUT inserted.IdHorario
                VALUES (@IdEmpleado, @DiaSemana, @HoraInicio, @HoraFin, 1, SYSDATETIME());
                """;
            await using var cmd = new SqlCommand(sql, cn, tx);
            AgregarHorario(cmd, idEmpleado, dto);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            await tx.CommitAsync(ct);
            return ResultadoOperacion<HorarioTrabajoDto>.Exito((await ObtenerHorarioAsync(id, ct))!);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task<ResultadoOperacion<HorarioTrabajoDto>> ActualizarHorarioAsync(
        int idHorario, GuardarHorarioRequestDto dto, CancellationToken ct = default)
    {
        var error = DisponibilidadReglas.ValidarHorario(dto.DiaSemana, dto.HoraInicio, dto.HoraFin);
        if (error is not null) return ResultadoOperacion<HorarioTrabajoDto>.Invalido(error);
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var actual = await ObtenerHorarioBaseAsync(cn, tx, idHorario, ct);
            if (actual is null)
                return await RevertirAsync(tx, ResultadoOperacion<HorarioTrabajoDto>.NoEncontrado("El horario no existe."), ct);
            if (actual.Activo)
            {
                var conflicto = await ConflictoHorarioAsync(cn, tx, actual.IdEmpleado, dto, idHorario, ct);
                if (conflicto is not null)
                    return await RevertirAsync(tx, ResultadoOperacion<HorarioTrabajoDto>.Conflicto(conflicto), ct);
            }
            const string sql = """
                UPDATE dbo.HorariosTrabajo SET DiaSemana=@DiaSemana, HoraInicio=@HoraInicio,
                    HoraFin=@HoraFin, FechaModificacion=SYSDATETIME() WHERE IdHorario=@IdHorario;
                """;
            await using var cmd = new SqlCommand(sql, cn, tx);
            AgregarHorario(cmd, actual.IdEmpleado, dto);
            cmd.Parameters.Add("@IdHorario", SqlDbType.Int).Value = idHorario;
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return ResultadoOperacion<HorarioTrabajoDto>.Exito((await ObtenerHorarioAsync(idHorario, ct))!);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task<ResultadoOperacion<HorarioTrabajoDto>> CambiarEstadoHorarioAsync(
        int idHorario, bool activo, CancellationToken ct = default)
    {
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var actual = await ObtenerHorarioBaseAsync(cn, tx, idHorario, ct);
            if (actual is null)
                return await RevertirAsync(tx, ResultadoOperacion<HorarioTrabajoDto>.NoEncontrado("El horario no existe."), ct);
            if (activo && !actual.Activo)
            {
                var dto = new GuardarHorarioRequestDto(actual.DiaSemana, actual.HoraInicio, actual.HoraFin);
                var conflicto = await ConflictoHorarioAsync(cn, tx, actual.IdEmpleado, dto, idHorario, ct);
                if (conflicto is not null)
                    return await RevertirAsync(tx, ResultadoOperacion<HorarioTrabajoDto>.Conflicto(conflicto), ct);
            }
            const string sql = "UPDATE dbo.HorariosTrabajo SET Activo=@Activo, FechaModificacion=SYSDATETIME() WHERE IdHorario=@IdHorario;";
            await using var cmd = new SqlCommand(sql, cn, tx);
            cmd.Parameters.Add("@Activo", SqlDbType.Bit).Value = activo;
            cmd.Parameters.Add("@IdHorario", SqlDbType.Int).Value = idHorario;
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return ResultadoOperacion<HorarioTrabajoDto>.Exito((await ObtenerHorarioAsync(idHorario, ct))!);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task<IReadOnlyList<DisponibilidadEmpleadoDto>> ConsultarDisponibilidadAsync(
        DisponibilidadConsultaDto consulta, CancellationToken ct = default)
    {
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        const string sql = """
            WITH EstadoPersonal AS (
                SELECT e.IdEmpleado,e.Codigo,CONCAT(e.Nombres,N' ',e.Apellidos) NombreCompleto,e.Cargo,e.Area,
                    CASE WHEN e.Activo=0 THEN N'Inactivo' WHEN b.IdBajaParcial IS NOT NULL THEN N'Baja Parcial'
                         WHEN h.IdHorario IS NULL THEN N'Fuera de horario' WHEN a.IdAsignacion IS NOT NULL THEN N'En Servicio'
                         ELSE N'Disponible' END EstadoDisponibilidad,
                    CASE WHEN e.Activo=0 THEN N'Empleado inactivo' WHEN b.IdBajaParcial IS NOT NULL THEN b.Motivo
                         WHEN h.IdHorario IS NULL THEN N'No existe una franja laboral aplicable'
                         WHEN a.IdAsignacion IS NOT NULL THEN CONCAT(N'Asignación: ',a.TipoTarea) ELSE N'Disponible para asignación' END Motivo,
                    CASE WHEN h.IdHorario IS NULL THEN NULL ELSE CONCAT(CONVERT(char(5),h.HoraInicio,108),N' - ',CONVERT(char(5),h.HoraFin,108)) END HorarioAplicable,
                    a.IdAsignacion,a.TipoTarea,a.FechaInicio,a.FechaFin,a.Estado EstadoAsignacion,a.Prioridad
                FROM dbo.Empleados e
                OUTER APPLY (SELECT TOP(1) * FROM dbo.BajasParciales b WHERE b.IdEmpleado=e.IdEmpleado AND b.Estado=N'Activa'
                    AND b.FechaInicio<=CAST(@FechaHora AS date) AND b.FechaFin>=CAST(@FechaHora AS date) ORDER BY b.IdBajaParcial DESC) b
                OUTER APPLY (SELECT TOP(1) * FROM dbo.HorariosTrabajo h WHERE h.IdEmpleado=e.IdEmpleado AND h.Activo=1
                    AND h.DiaSemana=@DiaSemana AND h.HoraInicio<=CAST(@FechaHora AS time) AND h.HoraFin>CAST(@FechaHora AS time) ORDER BY h.HoraInicio) h
                OUTER APPLY (SELECT TOP(1) * FROM dbo.AsignacionesTrabajo a WHERE a.IdEmpleado=e.IdEmpleado
                    AND a.Estado IN (N'Asignada',N'En Proceso') AND a.FechaInicio<=@FechaHora AND a.FechaFin>@FechaHora ORDER BY a.FechaInicio) a
                WHERE (@Busqueda IS NULL OR e.Codigo LIKE @Busqueda OR e.Nombres LIKE @Busqueda OR e.Apellidos LIKE @Busqueda)
                  AND (@Area IS NULL OR e.Area=@Area) AND (@Cargo IS NULL OR e.Cargo=@Cargo)
            ) SELECT * FROM EstadoPersonal WHERE @Estado IS NULL OR EstadoDisponibilidad=@Estado
              ORDER BY NombreCompleto, IdEmpleado;
            """;
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@FechaHora", SqlDbType.DateTime2).Value = consulta.FechaHora;
        cmd.Parameters.Add("@DiaSemana", SqlDbType.TinyInt).Value = DisponibilidadReglas.DiaIso(consulta.FechaHora.DayOfWeek);
        AgregarTexto(cmd, "@Busqueda", consulta.Busqueda, true, 200);
        AgregarTexto(cmd, "@Area", consulta.Area, false, 100);
        AgregarTexto(cmd, "@Cargo", consulta.Cargo, false, 100);
        AgregarTexto(cmd, "@Estado", consulta.Estado, false, 30);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<DisponibilidadEmpleadoDto>();
        while (await reader.ReadAsync(ct))
        {
            AsignacionActualDto? asignacion = reader["IdAsignacion"] is DBNull ? null : new(
                (int)reader["IdAsignacion"], (string)reader["TipoTarea"], (DateTime)reader["FechaInicio"],
                (DateTime)reader["FechaFin"], (string)reader["EstadoAsignacion"], (string)reader["Prioridad"]);
            lista.Add(new((int)reader["IdEmpleado"], (string)reader["Codigo"], (string)reader["NombreCompleto"],
                (string)reader["Cargo"], (string)reader["Area"], (string)reader["EstadoDisponibilidad"],
                (string)reader["Motivo"], DbString(reader, "HorarioAplicable"), asignacion));
        }
        return lista;
    }

    public async Task<PagedResult<AsignacionTrabajoResumenDto>> ObtenerAsignacionesAsync(
        AsignacionTrabajoConsultaDto consulta, CancellationToken ct = default)
    {
        var pagina = Math.Max(1, consulta.Pagina); var limite = Math.Clamp(consulta.Limite, 1, 100);
        const string where = """
            WHERE (@IdEmpleado IS NULL OR a.IdEmpleado=@IdEmpleado) AND (@IdCodigo IS NULL OR a.IdCodigo=@IdCodigo)
              AND (@Estado IS NULL OR a.Estado=@Estado) AND (@TipoTarea IS NULL OR a.TipoTarea=@TipoTarea)
              AND (@Fecha IS NULL OR (a.FechaInicio<DATEADD(day,1,@Fecha) AND a.FechaFin>=@Fecha))
            """;
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        await using var count = new SqlCommand("SELECT COUNT(*) FROM dbo.AsignacionesTrabajo a " + where, cn);
        AgregarFiltrosAsignacion(count, consulta);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        var sql = """
            SELECT a.IdAsignacion,a.IdEmpleado,e.Codigo CodigoEmpleado,CONCAT(e.Nombres,N' ',e.Apellidos) NombreEmpleado,
                   a.IdCodigo,COALESCE(c.CodF_SIG,CONVERT(nvarchar(30),c.CodFijo)) CodigoFijo,a.TipoTarea,a.FechaInicio,a.FechaFin,a.Estado,a.Prioridad
            FROM dbo.AsignacionesTrabajo a JOIN dbo.Empleados e ON e.IdEmpleado=a.IdEmpleado
            LEFT JOIN dbo.CodigosFijos c ON c.IdCodigo=a.IdCodigo
            """ + Environment.NewLine + where +
            " ORDER BY a.FechaInicio DESC,a.IdAsignacion DESC OFFSET @Offset ROWS FETCH NEXT @Limite ROWS ONLY;";
        await using var cmd = new SqlCommand(sql, cn); AgregarFiltrosAsignacion(cmd, consulta);
        cmd.Parameters.Add("@Offset", SqlDbType.Int).Value = (pagina - 1) * limite;
        cmd.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;
        await using var reader = await cmd.ExecuteReaderAsync(ct); var datos = new List<AsignacionTrabajoResumenDto>();
        while (await reader.ReadAsync(ct)) datos.Add(MapearAsignacionResumen(reader));
        return new() { Pagina=pagina, Limite=limite, TotalRegistros=total, Datos=datos };
    }

    public async Task<AsignacionTrabajoDetalleDto?> ObtenerAsignacionAsync(int idAsignacion, CancellationToken ct = default)
    {
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        return await ObtenerAsignacionAsync(cn, null, idAsignacion, false, ct);
    }

    public Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> CrearAsignacionAsync(GuardarAsignacionTrabajoDto dto, CancellationToken ct = default) =>
        GuardarAsignacionAsync(null, dto, ct);

    public Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> ActualizarAsignacionAsync(
        int idAsignacion, GuardarAsignacionTrabajoDto dto, CancellationToken ct = default) =>
        GuardarAsignacionAsync(idAsignacion, dto, ct);

    public async Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> CambiarEstadoAsignacionAsync(
        int idAsignacion, string estado, CancellationToken ct = default)
    {
        estado = estado?.Trim() ?? string.Empty;
        if (!CatalogosDisponibilidad.EstadosAsignacion.Contains(estado))
            return ResultadoOperacion<AsignacionTrabajoDetalleDto>.Invalido("El estado de la asignación no es válido.");
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var actual = await ObtenerAsignacionAsync(cn, tx, idAsignacion, true, ct);
            if (actual is null) return await RevertirAsync(tx, ResultadoOperacion<AsignacionTrabajoDetalleDto>.NoEncontrado("La asignación no existe."), ct);
            if (CatalogosDisponibilidad.Ocupa(estado))
            {
                var dto = new GuardarAsignacionTrabajoDto(actual.IdEmpleado, actual.IdCodigo, actual.TipoTarea, actual.FechaInicio,
                    actual.FechaFin, estado, actual.Prioridad, actual.Observaciones);
                var validacion = await ValidarAsignacionSqlAsync(cn, tx, dto, idAsignacion, ct);
                if (validacion.Tipo is not null)
                    return await RevertirAsync(tx, new ResultadoOperacion<AsignacionTrabajoDetalleDto>(validacion.Tipo.Value, null, validacion.Mensaje), ct);
            }
            await using var cmd = new SqlCommand("UPDATE dbo.AsignacionesTrabajo SET Estado=@Estado,FechaModificacion=SYSDATETIME() WHERE IdAsignacion=@Id;",cn,tx);
            cmd.Parameters.Add("@Estado",SqlDbType.NVarChar,20).Value=estado; cmd.Parameters.Add("@Id",SqlDbType.Int).Value=idAsignacion;
            await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
            return ResultadoOperacion<AsignacionTrabajoDetalleDto>.Exito((await ObtenerAsignacionAsync(idAsignacion,ct))!);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    private async Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> GuardarAsignacionAsync(
        int? idAsignacion, GuardarAsignacionTrabajoDto dto, CancellationToken ct)
    {
        var error = DisponibilidadReglas.ValidarAsignacion(dto);
        if (error is not null) return ResultadoOperacion<AsignacionTrabajoDetalleDto>.Invalido(error);
        await using var cn = await _connectionFactory.AbrirAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (idAsignacion.HasValue && await ObtenerAsignacionAsync(cn,tx,idAsignacion.Value,true,ct) is null)
                return await RevertirAsync(tx, ResultadoOperacion<AsignacionTrabajoDetalleDto>.NoEncontrado("La asignación no existe."), ct);
            var validacion = await ValidarAsignacionSqlAsync(cn, tx, dto, idAsignacion, ct);
            if (validacion.Tipo is not null)
                return await RevertirAsync(tx, new ResultadoOperacion<AsignacionTrabajoDetalleDto>(validacion.Tipo.Value,null,validacion.Mensaje),ct);
            int id;
            if (!idAsignacion.HasValue)
            {
                const string sql = """
                    INSERT dbo.AsignacionesTrabajo(IdEmpleado,IdCodigo,TipoTarea,FechaInicio,FechaFin,Estado,Prioridad,Observaciones,FechaRegistro)
                    OUTPUT inserted.IdAsignacion VALUES(@IdEmpleado,@IdCodigo,@TipoTarea,@Inicio,@Fin,@Estado,@Prioridad,@Obs,SYSDATETIME());
                    """;
                await using var cmd = new SqlCommand(sql,cn,tx); AgregarAsignacion(cmd,dto);
                id=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture);
            }
            else
            {
                const string sql = """
                    UPDATE dbo.AsignacionesTrabajo SET IdEmpleado=@IdEmpleado,IdCodigo=@IdCodigo,TipoTarea=@TipoTarea,
                      FechaInicio=@Inicio,FechaFin=@Fin,Estado=@Estado,Prioridad=@Prioridad,Observaciones=@Obs,FechaModificacion=SYSDATETIME()
                    WHERE IdAsignacion=@Id;
                    """;
                await using var cmd = new SqlCommand(sql,cn,tx); AgregarAsignacion(cmd,dto);
                cmd.Parameters.Add("@Id",SqlDbType.Int).Value=idAsignacion.Value; await cmd.ExecuteNonQueryAsync(ct); id=idAsignacion.Value;
            }
            await tx.CommitAsync(ct);
            return ResultadoOperacion<AsignacionTrabajoDetalleDto>.Exito((await ObtenerAsignacionAsync(id,ct))!);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    private async Task<(TipoResultadoOperacion? Tipo,string? Mensaje)> ValidarAsignacionSqlAsync(
        SqlConnection cn, SqlTransaction tx, GuardarAsignacionTrabajoDto dto, int? ignorarId, CancellationToken ct)
    {
        if (!await ExisteEmpleadoAsync(cn,tx,dto.IdEmpleado,false,ct))
            return (TipoResultadoOperacion.NoEncontrado,"El empleado no existe.");
        if (!await ExisteEmpleadoAsync(cn,tx,dto.IdEmpleado,true,ct))
            return (TipoResultadoOperacion.Conflicto,"El empleado se encuentra inactivo.");
        if (dto.IdCodigo.HasValue)
        {
            await using var codigo = new SqlCommand("SELECT COUNT(1) FROM dbo.CodigosFijos WHERE IdCodigo=@IdCodigo;",cn,tx);
            codigo.Parameters.Add("@IdCodigo",SqlDbType.Int).Value=dto.IdCodigo.Value;
            if (Convert.ToInt32(await codigo.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)==0)
                return (TipoResultadoOperacion.NoEncontrado,"El código fijo no existe.");
        }
        if (dto.FechaInicio.Date != dto.FechaFin.Date)
            return (TipoResultadoOperacion.Conflicto,"La asignación debe estar contenida dentro de una sola franja de trabajo.");
        const string horarioSql = """
            SELECT COUNT(1) FROM dbo.HorariosTrabajo WITH(UPDLOCK,HOLDLOCK) WHERE IdEmpleado=@IdEmpleado AND Activo=1
             AND DiaSemana=@Dia AND HoraInicio<=CAST(@Inicio AS time) AND HoraFin>=CAST(@Fin AS time);
            """;
        await using (var cmd = new SqlCommand(horarioSql,cn,tx))
        {
            cmd.Parameters.Add("@IdEmpleado",SqlDbType.Int).Value=dto.IdEmpleado;
            cmd.Parameters.Add("@Dia",SqlDbType.TinyInt).Value=DisponibilidadReglas.DiaIso(dto.FechaInicio.DayOfWeek);
            cmd.Parameters.Add("@Inicio",SqlDbType.DateTime2).Value=dto.FechaInicio; cmd.Parameters.Add("@Fin",SqlDbType.DateTime2).Value=dto.FechaFin;
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)==0)
                return (TipoResultadoOperacion.Conflicto,"La asignación debe estar contenida dentro de una franja activa de trabajo.");
        }
        const string bajaSql = """
            SELECT COUNT(1) FROM dbo.BajasParciales WITH(UPDLOCK,HOLDLOCK) WHERE IdEmpleado=@IdEmpleado AND Estado=N'Activa'
              AND @Inicio<DATEADD(day,1,CAST(FechaFin AS datetime2)) AND @Fin>CAST(FechaInicio AS datetime2);
            """;
        await using (var cmd = new SqlCommand(bajaSql,cn,tx))
        {
            cmd.Parameters.Add("@IdEmpleado",SqlDbType.Int).Value=dto.IdEmpleado; cmd.Parameters.Add("@Inicio",SqlDbType.DateTime2).Value=dto.FechaInicio;
            cmd.Parameters.Add("@Fin",SqlDbType.DateTime2).Value=dto.FechaFin;
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0)
                return (TipoResultadoOperacion.Conflicto,"El empleado posee una baja parcial durante el intervalo seleccionado.");
        }
        if (CatalogosDisponibilidad.Ocupa(dto.Estado))
        {
            var sql = "SELECT COUNT(1) FROM dbo.AsignacionesTrabajo WITH(UPDLOCK,HOLDLOCK) WHERE IdEmpleado=@IdEmpleado AND Estado IN(N'Asignada',N'En Proceso') AND @Inicio<FechaFin AND @Fin>FechaInicio" + (ignorarId.HasValue?" AND IdAsignacion<>@Ignorar":"") + ";";
            await using var cmd = new SqlCommand(sql,cn,tx); cmd.Parameters.Add("@IdEmpleado",SqlDbType.Int).Value=dto.IdEmpleado;
            cmd.Parameters.Add("@Inicio",SqlDbType.DateTime2).Value=dto.FechaInicio; cmd.Parameters.Add("@Fin",SqlDbType.DateTime2).Value=dto.FechaFin;
            if(ignorarId.HasValue) cmd.Parameters.Add("@Ignorar",SqlDbType.Int).Value=ignorarId.Value;
            if(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0)
                return (TipoResultadoOperacion.Conflicto,"El empleado ya posee una asignación durante ese intervalo.");
        }
        return (null,null);
    }

    private async Task<string?> ConflictoHorarioAsync(SqlConnection cn, SqlTransaction tx, int idEmpleado,
        GuardarHorarioRequestDto dto, int? ignorarId, CancellationToken ct)
    {
        var sql = """
            SELECT TOP(1) CASE WHEN HoraInicio=@Inicio AND HoraFin=@Fin THEN 1 ELSE 2 END
            FROM dbo.HorariosTrabajo WITH(UPDLOCK,HOLDLOCK) WHERE IdEmpleado=@IdEmpleado AND DiaSemana=@Dia AND Activo=1
              AND @Inicio<HoraFin AND @Fin>HoraInicio
            """ + (ignorarId.HasValue?" AND IdHorario<>@Ignorar":"") + " ORDER BY IdHorario;";
        await using var cmd = new SqlCommand(sql,cn,tx); AgregarHorario(cmd,idEmpleado,dto);
        if(ignorarId.HasValue) cmd.Parameters.Add("@Ignorar",SqlDbType.Int).Value=ignorarId.Value;
        var valor=await cmd.ExecuteScalarAsync(ct);
        return valor is null ? null : Convert.ToInt32(valor,CultureInfo.InvariantCulture)==1
            ? "Ya existe una franja idéntica para el empleado." : "El horario se solapa con otra franja activa del empleado.";
    }

    private async Task<HorarioTrabajoDto?> ObtenerHorarioAsync(int id, CancellationToken ct)
    { await using var cn=await _connectionFactory.AbrirAsync(ct); return await ObtenerHorarioBaseAsync(cn,null,id,ct); }
    private static async Task<HorarioTrabajoDto?> ObtenerHorarioBaseAsync(SqlConnection cn, SqlTransaction? tx,int id,CancellationToken ct)
    {
        var sql="SELECT IdHorario,IdEmpleado,DiaSemana,HoraInicio,HoraFin,Activo,FechaRegistro,FechaModificacion FROM dbo.HorariosTrabajo "+(tx is null?"":"WITH(UPDLOCK,HOLDLOCK) ")+"WHERE IdHorario=@Id;";
        await using var cmd=new SqlCommand(sql,cn,tx); cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;
        await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct)?MapearHorario(r):null;
    }
    private static async Task<bool> ExisteEmpleadoAsync(SqlConnection cn,SqlTransaction? tx,int id,bool activo,CancellationToken ct)
    { await using var cmd=new SqlCommand("SELECT COUNT(1) FROM dbo.Empleados "+(tx is null?"":"WITH(UPDLOCK,HOLDLOCK) ")+"WHERE IdEmpleado=@Id"+(activo?" AND Activo=1":"")+";",cn,tx); cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id; return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0; }
    private static async Task<AsignacionTrabajoDetalleDto?> ObtenerAsignacionAsync(SqlConnection cn,SqlTransaction? tx,int id,bool bloquear,CancellationToken ct)
    {
        var hint=bloquear?"WITH(UPDLOCK,HOLDLOCK) ":"";
        var sql=$"""
            SELECT a.IdAsignacion,a.IdEmpleado,e.Codigo CodigoEmpleado,CONCAT(e.Nombres,N' ',e.Apellidos) NombreEmpleado,a.IdCodigo,
            COALESCE(c.CodF_SIG,CONVERT(nvarchar(30),c.CodFijo)) CodigoFijo,c.Nombre NombreCodigoFijo,c.Longitud,c.Latitud,a.TipoTarea,a.FechaInicio,a.FechaFin,a.Estado,a.Prioridad,a.Observaciones,a.FechaRegistro,a.FechaModificacion
            FROM dbo.AsignacionesTrabajo a {hint} JOIN dbo.Empleados e ON e.IdEmpleado=a.IdEmpleado LEFT JOIN dbo.CodigosFijos c ON c.IdCodigo=a.IdCodigo WHERE a.IdAsignacion=@Id;
            """;
        await using var cmd=new SqlCommand(sql,cn,tx); cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id; await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct)?MapearAsignacionDetalle(r):null;
    }
    private static HorarioTrabajoDto MapearHorario(SqlDataReader r)=>new((int)r["IdHorario"],(int)r["IdEmpleado"],(byte)r["DiaSemana"],TimeOnly.FromTimeSpan((TimeSpan)r["HoraInicio"]),TimeOnly.FromTimeSpan((TimeSpan)r["HoraFin"]),(bool)r["Activo"],(DateTime)r["FechaRegistro"],DbDate(r,"FechaModificacion"));
    private static AsignacionTrabajoResumenDto MapearAsignacionResumen(SqlDataReader r)=>new((int)r["IdAsignacion"],(int)r["IdEmpleado"],(string)r["CodigoEmpleado"],(string)r["NombreEmpleado"],DbInt(r,"IdCodigo"),DbString(r,"CodigoFijo"),(string)r["TipoTarea"],(DateTime)r["FechaInicio"],(DateTime)r["FechaFin"],(string)r["Estado"],(string)r["Prioridad"]);
    private static AsignacionTrabajoDetalleDto MapearAsignacionDetalle(SqlDataReader r)=>new((int)r["IdAsignacion"],(int)r["IdEmpleado"],(string)r["CodigoEmpleado"],(string)r["NombreEmpleado"],DbInt(r,"IdCodigo"),DbString(r,"CodigoFijo"),DbString(r,"NombreCodigoFijo"),DbDouble(r,"Longitud"),DbDouble(r,"Latitud"),(string)r["TipoTarea"],(DateTime)r["FechaInicio"],(DateTime)r["FechaFin"],(string)r["Estado"],(string)r["Prioridad"],DbString(r,"Observaciones"),(DateTime)r["FechaRegistro"],DbDate(r,"FechaModificacion"));
    private static void AgregarHorario(SqlCommand c,int id,GuardarHorarioRequestDto d){c.Parameters.Add("@IdEmpleado",SqlDbType.Int).Value=id;c.Parameters.Add("@DiaSemana",SqlDbType.TinyInt).Value=d.DiaSemana;c.Parameters.Add("@Dia",SqlDbType.TinyInt).Value=d.DiaSemana;c.Parameters.Add("@HoraInicio",SqlDbType.Time).Value=d.HoraInicio.ToTimeSpan();c.Parameters.Add("@HoraFin",SqlDbType.Time).Value=d.HoraFin.ToTimeSpan();c.Parameters.Add("@Inicio",SqlDbType.Time).Value=d.HoraInicio.ToTimeSpan();c.Parameters.Add("@Fin",SqlDbType.Time).Value=d.HoraFin.ToTimeSpan();}
    private static void AgregarAsignacion(SqlCommand c,GuardarAsignacionTrabajoDto d){c.Parameters.Add("@IdEmpleado",SqlDbType.Int).Value=d.IdEmpleado;c.Parameters.Add("@IdCodigo",SqlDbType.Int).Value=(object?)d.IdCodigo??DBNull.Value;c.Parameters.Add("@TipoTarea",SqlDbType.NVarChar,30).Value=d.TipoTarea.Trim();c.Parameters.Add("@Inicio",SqlDbType.DateTime2).Value=d.FechaInicio;c.Parameters.Add("@Fin",SqlDbType.DateTime2).Value=d.FechaFin;c.Parameters.Add("@Estado",SqlDbType.NVarChar,20).Value=d.Estado.Trim();c.Parameters.Add("@Prioridad",SqlDbType.NVarChar,15).Value=d.Prioridad.Trim();c.Parameters.Add("@Obs",SqlDbType.NVarChar,500).Value=(object?)d.Observaciones?.Trim()??DBNull.Value;}
    private static void AgregarFiltrosAsignacion(SqlCommand c,AsignacionTrabajoConsultaDto d){c.Parameters.Add("@IdEmpleado",SqlDbType.Int).Value=(object?)d.IdEmpleado??DBNull.Value;c.Parameters.Add("@IdCodigo",SqlDbType.Int).Value=(object?)d.IdCodigo??DBNull.Value;AgregarTexto(c,"@Estado",d.Estado,false,20);AgregarTexto(c,"@TipoTarea",d.TipoTarea,false,30);c.Parameters.Add("@Fecha",SqlDbType.Date).Value=d.Fecha.HasValue?d.Fecha.Value.ToDateTime(TimeOnly.MinValue):DBNull.Value;}
    private static void AgregarTexto(SqlCommand c,string nombre,string? valor,bool like,int longitud)=>c.Parameters.Add(nombre,SqlDbType.NVarChar,longitud).Value=string.IsNullOrWhiteSpace(valor)?DBNull.Value:like?$"%{valor.Trim()}%":valor.Trim();
    private static string? DbString(SqlDataReader r,string n)=>r[n] is DBNull?null:(string)r[n]; private static int? DbInt(SqlDataReader r,string n)=>r[n] is DBNull?null:(int)r[n]; private static DateTime? DbDate(SqlDataReader r,string n)=>r[n] is DBNull?null:(DateTime)r[n]; private static double? DbDouble(SqlDataReader r,string n)=>r[n] is DBNull?null:Convert.ToDouble(r[n],CultureInfo.InvariantCulture);
    private static async Task<T> RevertirAsync<T>(SqlTransaction tx,T valor,CancellationToken ct){await tx.RollbackAsync(ct);return valor;}
}
