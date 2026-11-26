using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using FirebirdSql.Data.FirebirdClient;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Infrastructure.Firebird;

/// <summary>
/// Servicio de conectividad directa TCP/IP contra servidores Firebird (.GDB/.FDB)
/// con validación previa de puerto, consultas parametrizadas y soporte transaccional Commit/Rollback.
/// </summary>
public sealed class FirebirdSyncService : IFirebirdSyncService
{
    public static string BuildConnectionString(FirebirdPlantaEndpoint endpoint)
    {
        FbConnectionStringBuilder csb = new()
        {
            DataSource = endpoint.ServidorIp.Trim(),
            Port = endpoint.Puerto <= 0 ? 3050 : endpoint.Puerto,
            Database = NormalizeDatabasePath(endpoint.RutaArchivo),
            UserID = endpoint.Usuario.Trim(),
            Password = endpoint.Password,
            Dialect = 3,
            Charset = "UTF8",
            ConnectionTimeout = 5,
            Pooling = true
        };
        return csb.ToString();
    }

    public static string NormalizeDatabasePath(string rawPath)
    {
        string trimmed = rawPath.Trim();
        // Previene el error clásico UNC \\192.168.x.x\BDWM\...
        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            int idx = trimmed.IndexOf("BDWM", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return @"C:\" + trimmed[idx..].Replace('/', '\\');
            }
        }

        // Si incluye IP prefijada "192.168.2.56:\BDWM\...", extrae solo la ruta física local en servidor
        int colonIdx = trimmed.IndexOf(":\\", StringComparison.Ordinal);
        if (colonIdx > 1 && char.IsDigit(trimmed[0]))
        {
            return "C" + trimmed[colonIdx..];
        }

        return trimmed;
    }

    public async Task<(bool Success, int PingMs, int TablesCount, int ProceduresCount, string Message)> TestConnectionAsync(
        FirebirdPlantaEndpoint endpoint,
        CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            bool tcpOk = await ProbeTcpAsync(endpoint.ServidorIp, endpoint.Puerto, 2000, ct).ConfigureAwait(false);
            if (!tcpOk)
            {
                return (false, (int)sw.ElapsedMilliseconds, 0, 0,
                    $"No hay respuesta TCP en {endpoint.ServidorIp}:{endpoint.Puerto}. Verifique red/firewall o servicio Firebird.");
            }

            await using FbConnection conn = new(BuildConnectionString(endpoint));
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using FbCommand tblCmd = conn.CreateCommand();
            tblCmd.CommandText = "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0;";
            int tables = Convert.ToInt32(await tblCmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

            await using FbCommand procCmd = conn.CreateCommand();
            procCmd.CommandText = "SELECT COUNT(*) FROM RDB$PROCEDURES WHERE RDB$SYSTEM_FLAG = 0;";
            int procs = Convert.ToInt32(await procCmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

            sw.Stop();
            return (true, (int)sw.ElapsedMilliseconds, tables, procs,
                $"Conexión Firebird exitosa ({endpoint.ServidorIp}:{endpoint.Puerto}). {tables} tablas y {procs} procedimientos detectados.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, (int)sw.ElapsedMilliseconds, 0, 0, $"Error de conexión Firebird: {ex.Message}");
        }
    }

    public async Task<(IReadOnlyList<FirebirdRemotePesaje> Records, bool UsedSimulatedFallback, string StatusMessage)> QueryRemotePesajesAsync(
        FirebirdPlantaEndpoint endpoint,
        DateOnly desde,
        DateOnly hasta,
        bool allowFallbackDemo,
        CancellationToken ct = default)
    {
        try
        {
            bool tcpOk = await ProbeTcpAsync(endpoint.ServidorIp, endpoint.Puerto, 2000, ct).ConfigureAwait(false);
            if (!tcpOk)
            {
                if (allowFallbackDemo)
                {
                    return (GenerateSimulatedRecords(endpoint.Planta, desde), true,
                        $"Servidor {endpoint.ServidorIp}:{endpoint.Puerto} no accesible. Se cargaron registros de simulación offline.");
                }

                throw new InvalidOperationException($"No fue posible establecer conexión TCP con {endpoint.ServidorIp}:{endpoint.Puerto}.");
            }

            await using FbConnection conn = new(BuildConnectionString(endpoint));
            await conn.OpenAsync(ct).ConfigureAwait(false);

            List<FirebirdRemotePesaje> records = endpoint.Planta == Planta.Talcahuano
                ? await QueryTalcahuanoAsync(conn, desde, hasta, ct).ConfigureAwait(false)
                : await QueryPlacillaOrSantiagoAsync(conn, desde, hasta, ct).ConfigureAwait(false);

            return (records, false, $"Consulta real en Firebird ({endpoint.Nombre}) completada: {records.Count} registros.");
        }
        catch when (allowFallbackDemo)
        {
            return (GenerateSimulatedRecords(endpoint.Planta, desde), true,
                $"Sin acceso directo a {endpoint.RutaArchivo}. Cargados registros demostrativos de prueba.");
        }
    }

    public async Task<(bool Success, IReadOnlyList<string> Logs, string? ErrorMessage)> ExecuteStoredProceduresAsync(
        FirebirdPlantaEndpoint endpoint,
        bool allowSimulatedDemo,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        List<string> logs = [];
        void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            logs.Add(line);
            progress?.Report(line);
        }

        try
        {
            Log($"Verificando conectividad TCP hacia {endpoint.ServidorIp}:{endpoint.Puerto}...");
            bool tcpOk = await ProbeTcpAsync(endpoint.ServidorIp, endpoint.Puerto, 2000, ct).ConfigureAwait(false);

            if (!tcpOk)
            {
                if (!allowSimulatedDemo)
                {
                    string err = $"Servidor Firebird {endpoint.ServidorIp}:{endpoint.Puerto} no alcanzable.";
                    Log("ERROR: " + err);
                    return (false, logs, err);
                }

                Log("Servidor remoto no disponible; ejecutando simulación controlada de procedimientos...");
                await Task.Delay(250, ct).ConfigureAwait(false);
                Log("Paso 1/2 (Simulado): PA_INS_PROV_STGO ejecutado.");
                await Task.Delay(250, ct).ConfigureAwait(false);
                Log("Paso 2/2 (Simulado): PA_INS_MASIVO_EXP_SAP ejecutado.");
                Log("CommitTrans simulado completado.");
                return (true, logs, null);
            }

            Log($"Abriendo conexión Firebird a {NormalizeDatabasePath(endpoint.RutaArchivo)}...");
            await using FbConnection conn = new(BuildConnectionString(endpoint));
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using FbTransaction tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
            try
            {
                Log("Paso 1/2: Ejecutando PA_INS_PROV_STGO...");
                await using (FbCommand cmd1 = new("PA_INS_PROV_STGO", conn, tx) { CommandType = CommandType.StoredProcedure })
                {
                    await cmd1.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                Log("PA_INS_PROV_STGO completado.");

                Log("Paso 2/2: Ejecutando PA_INS_MASIVO_EXP_SAP...");
                await using (FbCommand cmd2 = new("PA_INS_MASIVO_EXP_SAP", conn, tx) { CommandType = CommandType.StoredProcedure })
                {
                    await cmd2.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                Log("PA_INS_MASIVO_EXP_SAP completado.");

                Log("Confirmando transacción (COMMIT)...");
                await tx.CommitAsync(ct).ConfigureAwait(false);
                Log("Transacción confirmada exitosamente.");
                return (true, logs, null);
            }
            catch (Exception ex)
            {
                Log($"ERROR durante ejecución: {ex.Message}");
                Log("Aplicando ROLLBACK para preservar integridad transaccional...");
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return (false, logs, ex.Message);
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR de conexión: {ex.Message}");
            return (false, logs, ex.Message);
        }
    }

    private static async Task<List<FirebirdRemotePesaje>> QueryPlacillaOrSantiagoAsync(
        FbConnection conn,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        const string sql = """
            SELECT p.flpesaje, p.tpesaje, p.patente, p.bruto, p.tara, p.neto,
                   p.producto, COALESCE(pr.descrip, p.producto),
                   p.cliente, COALESCE(cl.nombre, p.cliente),
                   COALESCE(o.descrip, CAST(p.origen AS VARCHAR(60))),
                   COALESCE(d.descrip, CAST(p.destino AS VARCHAR(60))),
                   p.gdespacho, p.fechaing, p.horaing, p.fechasal, p.horasal, p.observa1
            FROM dbpesajes p
            LEFT JOIN dbproductos pr ON pr.producto = p.producto
            LEFT JOIN dbclientes cl ON cl.rut = p.cliente
            LEFT JOIN dborigen o ON o.origen = p.origen
            LEFT JOIN dbdestinos d ON d.destino = p.destino
            WHERE p.fechaing >= @desde AND p.fechaing <= @hasta
            ORDER BY p.flpesaje
            """;

        await using FbCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@desde", desde.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@hasta", hasta.ToDateTime(TimeOnly.MaxValue));

        List<FirebirdRemotePesaje> list = [];
        await using FbDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            DateTime fing = rd.IsDBNull(13) ? DateTime.Today : Convert.ToDateTime(rd.GetValue(13), CultureInfo.InvariantCulture);
            DateTime fsal = rd.IsDBNull(15) ? fing : Convert.ToDateTime(rd.GetValue(15), CultureInfo.InvariantCulture);

            list.Add(new FirebirdRemotePesaje
            {
                Folio = rd.IsDBNull(0) ? 0 : Convert.ToInt64(rd.GetValue(0), CultureInfo.InvariantCulture),
                TipoPesajeRaw = rd.IsDBNull(1) ? "1" : rd.GetValue(1).ToString() ?? "1",
                Patente = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                PesoBruto = rd.IsDBNull(3) ? 0 : Convert.ToInt32(rd.GetValue(3), CultureInfo.InvariantCulture),
                PesoTara = rd.IsDBNull(4) ? 0 : Convert.ToInt32(rd.GetValue(4), CultureInfo.InvariantCulture),
                PesoNeto = rd.IsDBNull(5) ? 0 : Convert.ToInt32(rd.GetValue(5), CultureInfo.InvariantCulture),
                CodProducto = rd.IsDBNull(6) ? string.Empty : rd.GetString(6),
                NomProducto = rd.IsDBNull(7) ? string.Empty : rd.GetString(7),
                RutCliente = rd.IsDBNull(8) ? string.Empty : rd.GetString(8),
                NomCliente = rd.IsDBNull(9) ? string.Empty : rd.GetString(9),
                Origen = rd.IsDBNull(10) ? string.Empty : rd.GetString(10),
                Destino = rd.IsDBNull(11) ? string.Empty : rd.GetString(11),
                GuiaDespacho = rd.IsDBNull(12) ? string.Empty : rd.GetString(12),
                FechaIngreso = DateOnly.FromDateTime(fing),
                HoraIngreso = ParseFlexibleTime(rd.IsDBNull(14) ? null : rd.GetValue(14)),
                FechaSalida = DateOnly.FromDateTime(fsal),
                HoraSalida = ParseFlexibleTime(rd.IsDBNull(16) ? null : rd.GetValue(16)),
                Observacion = rd.IsDBNull(17) ? string.Empty : rd.GetString(17)
            });
        }
        return list;
    }

    private static async Task<List<FirebirdRemotePesaje>> QueryTalcahuanoAsync(
        FbConnection conn,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        const string sql = """
            SELECT pi.IDPSJ, pi.FECHA, pi.RUT, COALESCE(cp.NOMBRE, pi.RUT),
                   pi.CDGPRODUCTO, COALESCE(pr.PRODUCTO, pi.CDGPRODUCTO),
                   COALESCE(pe.PATENTE, ''), COALESCE(pi.GUIA, ''),
                   COALESCE(pe.TOTALNETO, pi.BRUTO), COALESCE(pi.BRUTO, 0)
            FROM PESAJES_ITEMS pi
            LEFT JOIN PESAJES pe ON pe.IDPSJ = pi.IDPSJ
            LEFT JOIN CLI_PROV cp ON cp.RUT = pi.RUT
            LEFT JOIN PRODUCTOS pr ON pr.CDGPRODUCTO = pi.CDGPRODUCTO
            WHERE pi.FECHA >= @desde AND pi.FECHA <= @hasta
            ORDER BY pi.IDPSJ, pi.ITEM
            """;

        await using FbCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@desde", desde.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@hasta", hasta.ToDateTime(TimeOnly.MaxValue));

        List<FirebirdRemotePesaje> list = [];
        await using FbDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            DateTime dt = rd.IsDBNull(1) ? DateTime.Today : Convert.ToDateTime(rd.GetValue(1), CultureInfo.InvariantCulture);
            int neto = rd.IsDBNull(8) ? 0 : (int)Math.Round(Convert.ToDecimal(rd.GetValue(8), CultureInfo.InvariantCulture));
            int bruto = rd.IsDBNull(9) ? neto : (int)Math.Round(Convert.ToDecimal(rd.GetValue(9), CultureInfo.InvariantCulture));

            list.Add(new FirebirdRemotePesaje
            {
                Folio = rd.IsDBNull(0) ? 0 : Convert.ToInt64(rd.GetValue(0), CultureInfo.InvariantCulture),
                TipoPesajeRaw = "1",
                Patente = rd.IsDBNull(6) ? string.Empty : rd.GetString(6),
                PesoBruto = bruto,
                PesoTara = Math.Max(0, bruto - neto),
                PesoNeto = neto,
                CodProducto = rd.IsDBNull(4) ? string.Empty : rd.GetString(4),
                NomProducto = rd.IsDBNull(5) ? string.Empty : rd.GetString(5),
                RutCliente = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                NomCliente = rd.IsDBNull(3) ? string.Empty : rd.GetString(3),
                Origen = "Talcahuano",
                Destino = "Bodega Thno",
                GuiaDespacho = rd.IsDBNull(7) ? string.Empty : rd.GetString(7),
                FechaIngreso = DateOnly.FromDateTime(dt),
                HoraIngreso = TimeOnly.FromDateTime(dt),
                FechaSalida = DateOnly.FromDateTime(dt),
                HoraSalida = TimeOnly.FromDateTime(dt),
                Observacion = "Importado PWAREHOUSE.FDB"
            });
        }
        return list;
    }

    private static async Task<bool> ProbeTcpAsync(string host, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using TcpClient client = new();
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host.Trim(), port <= 0 ? 3050 : port, cts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static TimeOnly ParseFlexibleTime(object? val)
    {
        if (val is null)
        {
            return TimeOnly.MinValue;
        }

        if (val is TimeSpan ts)
        {
            return TimeOnly.FromTimeSpan(ts);
        }

        if (val is DateTime dt)
        {
            return TimeOnly.FromDateTime(dt);
        }

        return TimeOnly.TryParse(val.ToString(), CultureInfo.InvariantCulture, out TimeOnly parsed)
            ? parsed
            : TimeOnly.MinValue;
    }

    private static List<FirebirdRemotePesaje> GenerateSimulatedRecords(Planta planta, DateOnly refDate) =>
        planta switch
        {
            Planta.Placilla =>
            [
                new() { Folio = 10430, TipoPesajeRaw = "1", Patente = "JK-9910", PesoBruto = 32500, PesoTara = 13200, PesoNeto = 19300, CodProducto = "5300000004", NomProducto = "SCRAP HDPE COMPRADO BLANCO", RutCliente = "76218764", NomCliente = "Sociedad Agrícola Demostrativa Ltda.", Origen = "Valparaíso", Destino = "Planta Placilla", GuiaDespacho = "GD-9001", FechaIngreso = refDate, HoraIngreso = new TimeOnly(8, 30), FechaSalida = refDate, HoraSalida = new TimeOnly(9, 15), Observacion = "Simulación Firebird Placilla" },
                new() { Folio = 10431, TipoPesajeRaw = "1", Patente = "LK-5544", PesoBruto = 28400, PesoTara = 12000, PesoNeto = 16400, CodProducto = "5301003001", NomProducto = "SCRAP TRANSPARENTE LIMPIO", RutCliente = "76218764", NomCliente = "Sociedad Agrícola Demostrativa Ltda.", Origen = "Quilpué", Destino = "Planta Placilla", GuiaDespacho = "GD-9002", FechaIngreso = refDate, HoraIngreso = new TimeOnly(10, 0), FechaSalida = refDate, HoraSalida = new TimeOnly(10, 40), Observacion = "Simulación Firebird Placilla" }
            ],
            Planta.Santiago =>
            [
                new() { Folio = 20560, TipoPesajeRaw = "1", Patente = "ST-1011", PesoBruto = 35000, PesoTara = 14200, PesoNeto = 20800, CodProducto = "5300000009", NomProducto = "SCRAP MIX SUPERMERCADO", RutCliente = "77321450", NomCliente = "Retail Supermercados Santiago S.A.", Origen = "Lo Aguirre", Destino = "Planta Santiago", GuiaDespacho = "CEN-991", FechaIngreso = refDate, HoraIngreso = new TimeOnly(7, 45), FechaSalida = refDate, HoraSalida = new TimeOnly(8, 30), Observacion = "Simulación Firebird Santiago" }
            ],
            _ =>
            [
                new() { Folio = 30110, TipoPesajeRaw = "1", Patente = "TK-8819", PesoBruto = 36000, PesoTara = 14000, PesoNeto = 22000, CodProducto = "5301001000", NomProducto = "SCRAP PESQUERA PRE LAVADO COLOR (SUP)", RutCliente = "76658930", NomCliente = "Pesquera del Pacífico Demo S.A.", Origen = "Talcahuano", Destino = "Bodega Thno", GuiaDespacho = "PESQ-990", FechaIngreso = refDate, HoraIngreso = new TimeOnly(9, 0), FechaSalida = refDate, HoraSalida = new TimeOnly(9, 45), Observacion = "Simulación PWAREHOUSE.FDB" }
            ]
        };
}
