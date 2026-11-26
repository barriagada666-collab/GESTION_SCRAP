using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Infrastructure.Firebird;

/// <summary>
/// Conector directo TCP/IP hacia Firebird (.GDB/.FDB) validado contra WMDBSPC.GDB y PWAREHOUSE.FDB.
/// Utiliza Charset=NONE + CodePages para compatibilidad exacta con bases Firebird heredadas y tildes/eñes.
/// </summary>
public sealed class FirebirdSyncService : IFirebirdSyncService
{
    static FirebirdSyncService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string BuildConnectionString(FirebirdPlantaEndpoint endpoint)
    {
        FbConnectionStringBuilder csb = new()
        {
            DataSource = endpoint.ServidorIp.Trim(),
            Port = endpoint.Puerto <= 0 ? 3050 : endpoint.Puerto,
            Database = NormalizeDatabasePath(endpoint.RutaArchivo),
            UserID = string.IsNullOrWhiteSpace(endpoint.Usuario) ? "SYSDBA" : endpoint.Usuario.Trim(),
            Password = endpoint.Password,
            Dialect = 3,
            Charset = "NONE",
            ConnectionTimeout = 8,
            Pooling = true
        };
        return csb.ToString();
    }

    public static string NormalizeDatabasePath(string rawPath)
    {
        string trimmed = rawPath.Trim();
        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            int idx = trimmed.IndexOf("BDWM", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return @"C:\" + trimmed[idx..].Replace('/', '\\');
            }
        }

        int colonIdx = trimmed.IndexOf(":\\", StringComparison.Ordinal);
        if (colonIdx > 1 && char.IsDigit(trimmed[0]))
        {
            return "C" + trimmed[colonIdx..];
        }

        return trimmed;
    }

    public async Task<(bool Success, int PingMs, int TablesCount, long PesajesCount, string DateBounds, string Message)> TestConnectionAsync(
        FirebirdPlantaEndpoint endpoint,
        CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            bool tcpOk = await ProbeTcpAsync(endpoint.ServidorIp, endpoint.Puerto, 2500, ct).ConfigureAwait(false);
            if (!tcpOk)
            {
                return (false, (int)sw.ElapsedMilliseconds, 0, 0, "-",
                    $"No hay respuesta TCP en {endpoint.ServidorIp}:{endpoint.Puerto}. Verifique red o servicio Firebird.");
            }

            await using FbConnection conn = new(BuildConnectionString(endpoint));
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using FbCommand tblCmd = conn.CreateCommand();
            tblCmd.CommandText = "SELECT COUNT(*) FROM RDB$RELATIONS WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0 AND RDB$VIEW_BLR IS NULL";
            int tables = Convert.ToInt32(await tblCmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

            long pesajesCount = 0;
            string bounds = "Sin registros";
            await using FbCommand statCmd = conn.CreateCommand();
            statCmd.CommandText = endpoint.Planta == Planta.Talcahuano
                ? "SELECT COUNT(*), MIN(FECHA), MAX(FECHA) FROM PESAJES_ITEMS"
                : "SELECT COUNT(*), MIN(FECHAING), MAX(FECHAING) FROM DBPESAJES WHERE COALESCE(ANULADO, '0') <> '1'";

            await using FbDataReader rd = await statCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                pesajesCount = rd.IsDBNull(0) ? 0L : Convert.ToInt64(rd.GetValue(0), CultureInfo.InvariantCulture);
                if (!rd.IsDBNull(1) && !rd.IsDBNull(2))
                {
                    DateTime minD = Convert.ToDateTime(rd.GetValue(1), CultureInfo.InvariantCulture);
                    DateTime maxD = Convert.ToDateTime(rd.GetValue(2), CultureInfo.InvariantCulture);
                    bounds = $"{minD:dd/MM/yyyy} -> {maxD:dd/MM/yyyy}";
                }
            }

            sw.Stop();
            return (true, (int)sw.ElapsedMilliseconds, tables, pesajesCount, bounds,
                $"Conexión Firebird exitosa ({endpoint.ServidorIp}:{endpoint.Puerto}). {tables} tablas · {pesajesCount:N0} pesajes ({bounds}).");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, (int)sw.ElapsedMilliseconds, 0, 0, "-", $"Error Firebird: {ex.Message}");
        }
    }

    public async Task<FirebirdExtractPackage> QueryRemotePackageAsync(
        FirebirdPlantaEndpoint endpoint,
        DateOnly? desde,
        DateOnly? hasta,
        bool allowFallbackDemo,
        CancellationToken ct = default)
    {
        try
        {
            bool tcpOk = await ProbeTcpAsync(endpoint.ServidorIp, endpoint.Puerto, 2500, ct).ConfigureAwait(false);
            if (!tcpOk)
            {
                if (allowFallbackDemo)
                {
                    return BuildFallbackDemoPackage(endpoint, desde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-14)));
                }

                throw new InvalidOperationException($"No fue posible establecer conexión TCP con {endpoint.ServidorIp}:{endpoint.Puerto}.");
            }

            await using FbConnection conn = new(BuildConnectionString(endpoint));
            await conn.OpenAsync(ct).ConfigureAwait(false);

            if (endpoint.Planta == Planta.Talcahuano)
            {
                return await QueryTalcahuanoPackageAsync(conn, desde, hasta, ct).ConfigureAwait(false);
            }

            return await QueryWmdbspcPackageAsync(conn, endpoint.Planta, desde, hasta, ct).ConfigureAwait(false);
        }
        catch when (allowFallbackDemo)
        {
            return BuildFallbackDemoPackage(endpoint, desde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-14)));
        }
    }

    private static async Task<FirebirdExtractPackage> QueryWmdbspcPackageAsync(
        FbConnection conn,
        Planta planta,
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken ct)
    {
        // 1. Catálogo de Clientes (DBCLIENTES)
        List<Cliente> clientes = [];
        const string sqlCli = """
            SELECT TRIM(rut), COALESCE(TRIM(digito), ''), TRIM(nombre),
                   COALESCE(TRIM(direccion), ''), COALESCE(TRIM(giro), ''),
                   COALESCE(TRIM(comuna), ''), COALESCE(TRIM(ciudad), ''), COALESCE(TRIM(telefono), '')
            FROM dbclientes
            ORDER BY rut
            """;
        await using (FbCommand cmd = new(sqlCli, conn))
        await using (FbDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                string rut = rd.IsDBNull(0) ? string.Empty : rd.GetString(0).Trim();
                if (string.IsNullOrWhiteSpace(rut))
                {
                    continue;
                }

                string nombre = rd.IsDBNull(2) ? rut : rd.GetString(2).Trim();
                clientes.Add(new Cliente
                {
                    Rut = rut,
                    Digito = rd.IsDBNull(1) ? string.Empty : rd.GetString(1).Trim(),
                    Nombre = nombre,
                    RazonSocial = nombre,
                    Direccion = rd.IsDBNull(3) ? string.Empty : rd.GetString(3).Trim(),
                    Giro = rd.IsDBNull(4) ? string.Empty : rd.GetString(4).Trim(),
                    Comuna = rd.IsDBNull(5) ? string.Empty : rd.GetString(5).Trim(),
                    Ciudad = rd.IsDBNull(6) ? string.Empty : rd.GetString(6).Trim(),
                    Telefono = rd.IsDBNull(7) ? string.Empty : rd.GetString(7).Trim(),
                    PlantaAsociada = planta
                });
            }
        }

        // 2. Catálogo de Productos (DBPRODUCTOS)
        List<Producto> productos = [];
        const string sqlProd = "SELECT TRIM(producto), COALESCE(TRIM(descrip), '') FROM dbproductos ORDER BY producto";
        await using (FbCommand cmd = new(sqlProd, conn))
        await using (FbDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                string cod = rd.IsDBNull(0) ? string.Empty : rd.GetString(0).Trim();
                if (string.IsNullOrWhiteSpace(cod))
                {
                    continue;
                }

                string desc = rd.IsDBNull(1) ? cod : rd.GetString(1).Trim();
                productos.Add(new Producto
                {
                    Codigo = cod,
                    Descripcion = desc,
                    Grupo = Application.Services.BusinessRules.ClassifyProductoDefaultGroup(cod, desc),
                    UnidadMedida = "KG"
                });
            }
        }

        // 3. Pesajes activos (DBPESAJES excluyendo anulados)
        string dateFilter = desde.HasValue && hasta.HasValue
            ? "AND p.fechaing >= @desde AND p.fechaing <= @hasta"
            : string.Empty;

        string sqlPes = $"""
            SELECT p.flpesaje, COALESCE(p.tpesaje, 1), COALESCE(TRIM(p.patente), ''), COALESCE(TRIM(p.acoplado), ''),
                   COALESCE(p.bruto, 0), COALESCE(p.tara, 0), COALESCE(p.neto, 0),
                   COALESCE(TRIM(p.producto), ''), COALESCE(TRIM(pr.descrip), TRIM(p.producto)),
                   COALESCE(TRIM(p.cliente), ''), COALESCE(TRIM(cl.nombre), TRIM(p.cliente)),
                   COALESCE(TRIM(o.descrip), CAST(COALESCE(p.origen, '') AS VARCHAR(60))),
                   COALESCE(TRIM(d.descrip), CAST(COALESCE(p.destino, '') AS VARCHAR(60))),
                   COALESCE(TRIM(p.gdespacho), ''), COALESCE(TRIM(p.chofer), ''), COALESCE(TRIM(tr.nombre), COALESCE(TRIM(p.transp), '')),
                   p.fechaing, p.horaing, p.fechasal, p.horasal, COALESCE(TRIM(p.observa1), '')
            FROM dbpesajes p
            LEFT JOIN dbproductos pr ON pr.producto = p.producto
            LEFT JOIN dbclientes cl ON cl.rut = p.cliente
            LEFT JOIN dborigen o ON o.origen = p.origen
            LEFT JOIN dbdestinos d ON d.destino = p.destino
            LEFT JOIN dbtransp tr ON tr.transp = p.transp
            WHERE COALESCE(p.anulado, '0') <> '1'
              {dateFilter}
            ORDER BY p.flpesaje
            """;

        await using FbCommand pesCmd = new(sqlPes, conn);
        if (desde.HasValue && hasta.HasValue)
        {
            pesCmd.Parameters.AddWithValue("@desde", desde.Value.ToDateTime(TimeOnly.MinValue));
            pesCmd.Parameters.AddWithValue("@hasta", hasta.Value.ToDateTime(TimeOnly.MaxValue));
        }

        List<FirebirdRemotePesaje> pesajes = [];
        await using FbDataReader prd = await pesCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await prd.ReadAsync(ct).ConfigureAwait(false))
        {
            DateTime fing = prd.IsDBNull(16) ? DateTime.Today : Convert.ToDateTime(prd.GetValue(16), CultureInfo.InvariantCulture);
            DateTime fsal = prd.IsDBNull(18) ? fing : Convert.ToDateTime(prd.GetValue(18), CultureInfo.InvariantCulture);

            pesajes.Add(new FirebirdRemotePesaje
            {
                Folio = prd.IsDBNull(0) ? 0 : Convert.ToInt64(prd.GetValue(0), CultureInfo.InvariantCulture),
                TipoPesajeRaw = prd.IsDBNull(1) ? "1" : prd.GetValue(1).ToString() ?? "1",
                Patente = prd.IsDBNull(2) ? string.Empty : prd.GetString(2),
                Acoplado = prd.IsDBNull(3) ? string.Empty : prd.GetString(3),
                PesoBruto = prd.IsDBNull(4) ? 0 : Convert.ToInt32(prd.GetValue(4), CultureInfo.InvariantCulture),
                PesoTara = prd.IsDBNull(5) ? 0 : Convert.ToInt32(prd.GetValue(5), CultureInfo.InvariantCulture),
                PesoNeto = prd.IsDBNull(6) ? 0 : Convert.ToInt32(prd.GetValue(6), CultureInfo.InvariantCulture),
                CodProducto = prd.IsDBNull(7) ? string.Empty : prd.GetString(7),
                NomProducto = prd.IsDBNull(8) ? string.Empty : prd.GetString(8),
                RutCliente = prd.IsDBNull(9) ? string.Empty : prd.GetString(9),
                NomCliente = prd.IsDBNull(10) ? string.Empty : prd.GetString(10),
                Origen = prd.IsDBNull(11) ? string.Empty : prd.GetString(11),
                Destino = prd.IsDBNull(12) ? string.Empty : prd.GetString(12),
                GuiaDespacho = prd.IsDBNull(13) ? string.Empty : prd.GetString(13),
                Chofer = prd.IsDBNull(14) ? string.Empty : prd.GetString(14),
                Transportista = prd.IsDBNull(15) ? string.Empty : prd.GetString(15),
                FechaIngreso = DateOnly.FromDateTime(fing),
                HoraIngreso = ParseFlexibleTime(prd.IsDBNull(17) ? null : prd.GetValue(17)),
                FechaSalida = DateOnly.FromDateTime(fsal),
                HoraSalida = ParseFlexibleTime(prd.IsDBNull(19) ? null : prd.GetValue(19)),
                Observacion = prd.IsDBNull(20) ? string.Empty : prd.GetString(20)
            });
        }

        return new FirebirdExtractPackage(
            pesajes,
            clientes,
            productos,
            false,
            $"Extracción Firebird completada: {pesajes.Count:N0} pesajes, {clientes.Count:N0} clientes y {productos.Count:N0} productos.");
    }

    private static async Task<FirebirdExtractPackage> QueryTalcahuanoPackageAsync(
        FbConnection conn,
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken ct)
    {
        List<Cliente> clientes = [];
        await using (FbCommand cCmd = new("SELECT TRIM(RUT), COALESCE(TRIM(NOMBRE), TRIM(RUT)) FROM CLI_PROV ORDER BY RUT", conn))
        await using (FbDataReader crd = await cCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await crd.ReadAsync(ct).ConfigureAwait(false))
            {
                string rut = crd.IsDBNull(0) ? string.Empty : crd.GetString(0).Trim();
                if (!string.IsNullOrEmpty(rut))
                {
                    string nom = crd.IsDBNull(1) ? rut : crd.GetString(1).Trim();
                    clientes.Add(new Cliente { Rut = rut, Nombre = nom, RazonSocial = nom, Ciudad = "Talcahuano", PlantaAsociada = Planta.Talcahuano });
                }
            }
        }

        List<Producto> productos = [];
        await using (FbCommand pCmd = new("SELECT TRIM(CDGPRODUCTO), COALESCE(TRIM(PRODUCTO), TRIM(CDGPRODUCTO)) FROM PRODUCTOS ORDER BY CDGPRODUCTO", conn))
        await using (FbDataReader prd = await pCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await prd.ReadAsync(ct).ConfigureAwait(false))
            {
                string cod = prd.IsDBNull(0) ? string.Empty : prd.GetString(0).Trim();
                if (!string.IsNullOrEmpty(cod))
                {
                    string nom = prd.IsDBNull(1) ? cod : prd.GetString(1).Trim();
                    productos.Add(new Producto
                    {
                        Codigo = cod,
                        Descripcion = nom,
                        Grupo = Application.Services.BusinessRules.ClassifyProductoDefaultGroup(cod, nom)
                    });
                }
            }
        }

        string dateFilter = desde.HasValue && hasta.HasValue ? "WHERE pi.FECHA >= @desde AND pi.FECHA <= @hasta" : string.Empty;
        string sql = $"""
            SELECT pi.IDPSJ, pi.FECHA, COALESCE(TRIM(pi.RUT), ''), COALESCE(TRIM(cp.NOMBRE), TRIM(pi.RUT)),
                   COALESCE(TRIM(pi.CDGPRODUCTO), ''), COALESCE(TRIM(pr.PRODUCTO), TRIM(pi.CDGPRODUCTO)),
                   COALESCE(TRIM(pe.PATENTE), ''), COALESCE(TRIM(pi.GUIA), ''),
                   COALESCE(pe.TOTALNETO, pi.BRUTO), COALESCE(pi.BRUTO, 0)
            FROM PESAJES_ITEMS pi
            LEFT JOIN PESAJES pe ON pe.IDPSJ = pi.IDPSJ
            LEFT JOIN CLI_PROV cp ON cp.RUT = pi.RUT
            LEFT JOIN PRODUCTOS pr ON pr.CDGPRODUCTO = pi.CDGPRODUCTO
            {dateFilter}
            ORDER BY pi.IDPSJ, pi.ITEM
            """;

        await using FbCommand cmd = new(sql, conn);
        if (desde.HasValue && hasta.HasValue)
        {
            cmd.Parameters.AddWithValue("@desde", desde.Value.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("@hasta", hasta.Value.ToDateTime(TimeOnly.MaxValue));
        }

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
                Destino = "Planta Talcahuano",
                GuiaDespacho = rd.IsDBNull(7) ? string.Empty : rd.GetString(7),
                FechaIngreso = DateOnly.FromDateTime(dt),
                HoraIngreso = new TimeOnly(dt.Hour, dt.Minute),
                FechaSalida = DateOnly.FromDateTime(dt),
                HoraSalida = new TimeOnly(dt.Hour, dt.Minute)
            });
        }

        return new FirebirdExtractPackage(
            list,
            clientes,
            productos,
            false,
            $"Extracción Talcahuano completada: {list.Count:N0} pesajes, {clientes.Count:N0} clientes y {productos.Count:N0} productos.");
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
            return new TimeOnly(8, 0);
        }

        string s = val.ToString()?.Trim() ?? string.Empty;
        if (TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out TimeOnly t))
        {
            return t;
        }

        return new TimeOnly(8, 0);
    }

    private static FirebirdExtractPackage BuildFallbackDemoPackage(FirebirdPlantaEndpoint ep, DateOnly baseDate)
    {
        FirebirdRemotePesaje[] pesajes =
        [
            new()
            {
                Folio = 9501, TipoPesajeRaw = "1", Patente = "DEMO01", PesoBruto = 22400, PesoTara = 9200, PesoNeto = 13200,
                CodProducto = "5300000009", NomProducto = "SCRAP MIX SUPERMERCADO", RutCliente = "89201400", NomCliente = "ENV. IMPRESOS CORDILLERA SPA",
                Origen = "BOD. VICKY", Destino = "PLACILLA", FechaIngreso = baseDate, HoraIngreso = new TimeOnly(9, 15),
                FechaSalida = baseDate, HoraSalida = new TimeOnly(9, 45), Observacion = "Registro de simulación offline"
            }
        ];

        return new FirebirdExtractPackage(
            pesajes,
            [],
            [],
            true,
            $"Servidor {ep.ServidorIp}:{ep.Puerto} no accesible; cargado 1 registro de demostración.");
    }
}
