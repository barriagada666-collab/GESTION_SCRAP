using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Infrastructure.Migration;

/// <summary>
/// Servicio de importación y migración desde bases heredadas Microsoft Access (.mdb / .accdb) hacia SQLite.
/// Intenta lectura directa con OleDb en el proceso actual (x64) y, si solo existe el proveedor ACE/Jet de 32 bits,
/// delega automáticamente la extracción de solo lectura a Windows PowerShell x86 (SysWOW64).
/// </summary>
public sealed class MdbMigrationService(IScrapRepository repository) : IMdbMigrationService
{
    private readonly IScrapRepository _repository = repository;

    private static readonly string[] AllowedTables =
    [
        "PRODUCTO_GRUPO",
        "PRODUCTO_GRUPO2",
        "PRODUCTO_GRUPO3",
        "PRODUCTO_GRUPOTHNO",
        "PRODUCTOS_CONS",
        "PRODUCTO",
        "CLIENTES",
        "CLIENTES_PLACILLA",
        "PESAJE_PLACILLA_2",
        "PESAJE_SANTIAGO",
        "PESAJE_TALCAHUANO",
        "PESAJE_PLACILLA",
        "PRECIOS_PROVEEDOR_PLACILLA",
        "PRECIOS_PROVEEDOR_STGO",
        "INDEX_PESAJE_PLACILLA",
        "INDEX_PESAJE_SANTIAGO",
        "ULTIMO_FOLIO"
    ];

    public async Task<(int PesajesImported, int ClientesImported, int ProductosImported, int PreciosImported, int IndexPreciosImported, IReadOnlyList<string> Warnings)>
        ImportFromMdbAsync(string mdbPath, string usuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mdbPath) || !File.Exists(mdbPath))
        {
            throw new FileNotFoundException("No se encontró el archivo de base de datos Access (.mdb/.accdb) especificado.", mdbPath);
        }

        List<string> warnings = [];
        Dictionary<string, List<Dictionary<string, string>>> rawTables =
            await ReadTablesWithAutoFallbackAsync(mdbPath, warnings, ct).ConfigureAwait(false);

        // 1. Productos y mapeo de grupos
        Dictionary<string, Producto> productosMap = new(StringComparer.OrdinalIgnoreCase);
        void LoadCatalogTable(string tableName, string codeCol, string descCol, string? grpCol)
        {
            if (!rawTables.TryGetValue(tableName, out var rows))
            {
                return;
            }

            foreach (var r in rows)
            {
                string code = GetStr(r, codeCol).ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                string desc = GetStr(r, descCol).ToUpperInvariant();
                string grp = grpCol is not null ? GetStr(r, grpCol).ToUpperInvariant() : string.Empty;
                if (string.IsNullOrWhiteSpace(grp))
                {
                    grp = BusinessRules.ClassifyGrupoMaterial(code, desc);
                }

                productosMap[code] = new Producto
                {
                    Codigo = code,
                    Descripcion = string.IsNullOrWhiteSpace(desc) ? code : desc,
                    Grupo = grp,
                    UnidadMedida = "KG"
                };
            }
        }

        LoadCatalogTable("PRODUCTO", "PRD_CODIGO", "PRD_DESCRIPCION", null);
        LoadCatalogTable("PRODUCTOS_CONS", "ID_PRODUCTO", "DESC_PRODUCTO", "grupo");
        LoadCatalogTable("PRODUCTO_GRUPOTHNO", "prd_codigo", "prd_descripcion", "grupo");
        LoadCatalogTable("PRODUCTO_GRUPO", "prd_codigo", "prd_descripcion", "grupo");

        if (productosMap.Count > 0)
        {
            await _repository.UpsertProductosAsync(productosMap.Values, usuario, ct).ConfigureAwait(false);
        }

        Dictionary<string, string> groupLookup = productosMap.ToDictionary(kv => kv.Key, kv => kv.Value.Grupo, StringComparer.OrdinalIgnoreCase);

        // 2. Clientes
        List<Cliente> clientes = [];
        if (rawTables.TryGetValue("CLIENTES", out var stgoClients))
        {
            foreach (var r in stgoClients)
            {
                string rut = GetStr(r, "RUT");
                if (string.IsNullOrWhiteSpace(rut))
                {
                    continue;
                }

                clientes.Add(new Cliente
                {
                    Rut = rut,
                    Digito = GetStr(r, "DIGITO"),
                    Nombre = GetStr(r, "NOMBRE"),
                    RazonSocial = GetStr(r, "NOMBRE"),
                    Ciudad = GetStr(r, "CIUDAD"),
                    Direccion = GetStr(r, "DIRECCION"),
                    PlantaAsociada = Planta.Santiago
                });
            }
        }

        if (rawTables.TryGetValue("CLIENTES_PLACILLA", out var plClients))
        {
            foreach (var r in plClients)
            {
                string rut = GetStr(r, "CPV_RUT");
                if (string.IsNullOrWhiteSpace(rut))
                {
                    continue;
                }

                string rs = GetStr(r, "CPV_RAZONSOCIAL");
                clientes.Add(new Cliente
                {
                    Rut = rut,
                    Digito = string.Empty,
                    Nombre = rs,
                    RazonSocial = rs,
                    Ciudad = GetStr(r, "CPV_CIUDAD"),
                    Direccion = GetStr(r, "CPV_DIRECCION"),
                    PlantaAsociada = Planta.Placilla
                });
            }
        }

        if (clientes.Count > 0)
        {
            await _repository.UpsertClientesAsync(clientes, usuario, ct).ConfigureAwait(false);
        }

        // 3. Pesajes por planta
        int totalPesajes = 0;
        totalPesajes += await ImportStandardPesajeTableAsync("PESAJE_PLACILLA_2", Planta.Placilla, rawTables, groupLookup, usuario, ct).ConfigureAwait(false);
        totalPesajes += await ImportStandardPesajeTableAsync("PESAJE_SANTIAGO", Planta.Santiago, rawTables, groupLookup, usuario, ct).ConfigureAwait(false);
        totalPesajes += await ImportStandardPesajeTableAsync("PESAJE_TALCAHUANO", Planta.Talcahuano, rawTables, groupLookup, usuario, ct).ConfigureAwait(false);

        // 4. Precios proveedor
        int preciosCount = 0;
        preciosCount += await ImportPreciosProveedorTableAsync("PRECIOS_PROVEEDOR_PLACILLA", Planta.Placilla, rawTables, usuario, ct).ConfigureAwait(false);
        preciosCount += await ImportPreciosProveedorTableAsync("PRECIOS_PROVEEDOR_STGO", Planta.Santiago, rawTables, usuario, ct).ConfigureAwait(false);

        // 5. Index precios reales
        List<IndexPrecioPesaje> indexItems = [];
        CollectIndexPrecios("INDEX_PESAJE_PLACILLA", Planta.Placilla, rawTables, indexItems);
        CollectIndexPrecios("INDEX_PESAJE_SANTIAGO", Planta.Santiago, rawTables, indexItems);
        int idxCount = 0;
        if (indexItems.Count > 0)
        {
            idxCount = await _repository.BulkFreezeIndexPreciosAsync(indexItems, usuario, ct).ConfigureAwait(false);
        }

        // 6. Último folio JCRAMM
        if (rawTables.TryGetValue("ULTIMO_FOLIO", out var ufRows) && ufRows.Count > 0)
        {
            long f = ParseLong(GetStr(ufRows[0], "FOLIO"));
            DateOnly dt = ParseDate(GetStr(ufRows[0], "FECHA"));
            if (f > 0)
            {
                await _repository.SetUltimoFolioJcrammAsync(f, dt, usuario, ct).ConfigureAwait(false);
            }
        }

        await _repository.AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = usuario,
            Accion = "MIGRACION_MDB",
            Entidad = "ACCESS_MDB",
            RegistroId = Path.GetFileName(mdbPath),
            Detalle = $"Migración Access completada: {totalPesajes} pesajes, {clientes.Count} clientes, {productosMap.Count} productos, {preciosCount} precios."
        }, ct).ConfigureAwait(false);

        return (totalPesajes, clientes.Count, productosMap.Count, preciosCount, idxCount, warnings);
    }

    private async Task<int> ImportStandardPesajeTableAsync(
        string tableName,
        Planta planta,
        Dictionary<string, List<Dictionary<string, string>>> rawTables,
        Dictionary<string, string> groupLookup,
        string usuario,
        CancellationToken ct)
    {
        if (!rawTables.TryGetValue(tableName, out var rows) || rows.Count == 0)
        {
            return 0;
        }

        List<Pesaje> items = [];
        foreach (var r in rows)
        {
            long folio = ParseLong(GetStr(r, "FOLIO"));
            if (folio <= 0)
            {
                continue;
            }

            string codProd = GetStr(r, "COD_PRODUCTO").ToUpperInvariant();
            string nomProd = GetStr(r, "NOM_PRODUCTO").ToUpperInvariant();
            string rut = GetStr(r, "RUT_CLIENTE");
            string tipoRaw = GetStr(r, "TIPO_PESAJE");
            TipoPesaje tipo = TipoPesajeExtensions.FromLegacyValue(tipoRaw);

            int bruto = ParseInt(GetStr(r, "PESO_BRUTO"));
            int tara = ParseInt(GetStr(r, "PESO_TARA"));
            int neto = ParseInt(GetStr(r, "PESO_NETO"));
            if (neto <= 0 && bruto > tara)
            {
                neto = bruto - tara;
            }

            DateOnly fecIng = ParseDate(GetStr(r, "FECHA_INGRESO"));
            DateOnly fecSal = ParseDate(GetStr(r, "FECHA_SALIDA"), fecIng);

            Pesaje p = new()
            {
                Folio = folio,
                Planta = planta,
                TipoPesaje = tipo,
                Patente = GetStr(r, "PATENTE").ToUpperInvariant(),
                PesoBruto = bruto,
                PesoTara = tara,
                PesoNeto = neto,
                CodProducto = codProd,
                NomProducto = nomProd,
                RutCliente = rut,
                NomCliente = GetStr(r, "NOM_CLIENTE"),
                Origen = GetStr(r, "ORIGEN"),
                Destino = GetStr(r, "DESTINO"),
                GuiaDespacho = GetStr(r, "GUIA_DESP"),
                FechaIngreso = fecIng,
                HoraIngreso = ParseTime(GetStr(r, "HORA_INGRESO")),
                FechaSalida = fecSal,
                HoraSalida = ParseTime(GetStr(r, "HORA_SALIDA")),
                Observacion = GetStr(r, "OBSERVACION"),
                GrupoMaterial = BusinessRules.ClassifyGrupoMaterial(codProd, nomProd, groupLookup),
                EsTraslado = tipo != TipoPesaje.RecepcionCarga
            };

            items.Add(new Pesaje
            {
                Folio = p.Folio,
                Planta = p.Planta,
                TipoPesaje = p.TipoPesaje,
                Patente = p.Patente,
                PesoBruto = p.PesoBruto,
                PesoTara = p.PesoTara,
                PesoNeto = p.PesoNeto,
                CodProducto = p.CodProducto,
                NomProducto = p.NomProducto,
                RutCliente = p.RutCliente,
                NomCliente = p.NomCliente,
                Origen = p.Origen,
                Destino = p.Destino,
                GuiaDespacho = p.GuiaDespacho,
                FechaIngreso = p.FechaIngreso,
                HoraIngreso = p.HoraIngreso,
                FechaSalida = p.FechaSalida,
                HoraSalida = p.HoraSalida,
                Observacion = p.Observacion,
                GrupoMaterial = p.GrupoMaterial,
                EsTraslado = p.EsTraslado || BusinessRules.IsExcludedInternalTransferClient(p)
            });
        }

        (int imported, _) = await _repository.ImportPesajesAsync(items, planta, usuario, ct).ConfigureAwait(false);
        return imported;
    }

    private async Task<int> ImportPreciosProveedorTableAsync(
        string tableName,
        Planta planta,
        Dictionary<string, List<Dictionary<string, string>>> rawTables,
        string usuario,
        CancellationToken ct)
    {
        if (!rawTables.TryGetValue(tableName, out var rows) || rows.Count == 0)
        {
            return 0;
        }

        int count = 0;
        foreach (var r in rows)
        {
            string rut = GetStr(r, "CPV_RUT");
            string cod = GetStr(r, "PRD_CODIGO").ToUpperInvariant();
            decimal precio = ParseDecimal(GetStr(r, "PRECIO"));
            if (string.IsNullOrWhiteSpace(rut) || string.IsNullOrWhiteSpace(cod) || precio <= 0)
            {
                continue;
            }

            await _repository.UpsertPrecioProveedorAsync(new PrecioProveedor
            {
                Planta = planta,
                Rut = rut,
                RazonSocial = GetStr(r, "CPV_RAZONSOCIAL"),
                CodigoProducto = cod,
                Precio = precio,
                FechaActualizacion = ParseDate(GetStr(r, "FECHA_ACTUALIZACION"))
            }, allowUpdate: true, usuario, ct).ConfigureAwait(false);
            count++;
        }

        return count;
    }

    private static void CollectIndexPrecios(
        string tableName,
        Planta planta,
        Dictionary<string, List<Dictionary<string, string>>> rawTables,
        List<IndexPrecioPesaje> target)
    {
        if (!rawTables.TryGetValue(tableName, out var rows))
        {
            return;
        }

        foreach (var r in rows)
        {
            long folio = ParseLong(GetStr(r, "PES_NUM"));
            decimal precio = ParseDecimal(GetStr(r, "PES_NUMCOR"));
            if (folio <= 0 || precio <= 0)
            {
                continue;
            }

            target.Add(new IndexPrecioPesaje
            {
                Folio = folio,
                Planta = planta,
                Precio = precio,
                Familia = GetStr(r, "prd_familia"),
                ActualizadoEn = DateTime.UtcNow
            });
        }
    }

    private static async Task<Dictionary<string, List<Dictionary<string, string>>>> ReadTablesWithAutoFallbackAsync(
        string mdbPath,
        List<string> warnings,
        CancellationToken ct)
    {
        try
        {
            return ReadTablesInProcess(mdbPath, warnings);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("registered", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("registrado", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("provider", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("proveedor", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Proveedor ACE/Jet de 64 bits no detectado; usando puente lector x86 compatible.");
            return await ReadTablesViaWow64BridgeAsync(mdbPath, warnings, ct).ConfigureAwait(false);
        }
    }

    private static Dictionary<string, List<Dictionary<string, string>>> ReadTablesInProcess(
        string mdbPath,
        List<string> warnings)
    {
        string[] providers = ["Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0", "Microsoft.Jet.OLEDB.4.0"];
        OleDbConnection? openConn = null;

        foreach (string prov in providers)
        {
            try
            {
                OleDbConnection candidate = new($"Provider={prov};Data Source={mdbPath};");
                candidate.Open();
                openConn = candidate;
                break;
            }
            catch (InvalidOperationException)
            {
                // Try next provider; rethrow below if none registered
            }
            catch
            {
                // Try next provider
            }
        }

        if (openConn is null)
        {
            throw new InvalidOperationException("No OLEDB provider is registered for Access MDB in this process architecture.");
        }

        using (openConn)
        {
            Dictionary<string, List<Dictionary<string, string>>> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (string table in AllowedTables)
            {
                try
                {
                    using OleDbCommand cmd = openConn.CreateCommand();
                    cmd.CommandText = $"SELECT * FROM [{table}]";
                    using OleDbDataReader rd = cmd.ExecuteReader();
                    List<Dictionary<string, string>> rows = [];
                    while (rd.Read())
                    {
                        Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < rd.FieldCount; i++)
                        {
                            object val = rd.GetValue(i);
                            row[rd.GetName(i)] = val is DBNull ? string.Empty : Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
                        }
                        rows.Add(row);
                    }
                    result[table] = rows;
                }
                catch (Exception ex)
                {
                    warnings.Add($"Tabla '{table}' omitida: {ex.Message}");
                }
            }
            return result;
        }
    }

    private static async Task<Dictionary<string, List<Dictionary<string, string>>>> ReadTablesViaWow64BridgeAsync(
        string mdbPath,
        List<string> warnings,
        CancellationToken ct)
    {
        string ps86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(ps86))
        {
            throw new PlatformNotSupportedException("No se encontró el subsistema PowerShell x86 ni proveedor ACE OLEDB x64.");
        }

        string tableListLiteral = string.Join(",", AllowedTables.Select(t => $"'{t}'"));
        string escapedPath = mdbPath.Replace("'", "''", StringComparison.Ordinal);

        string script = $$"""
            $ErrorActionPreference = 'Stop'
            $mdb = '{{escapedPath}}'
            $tables = @({{tableListLiteral}})
            $conn = $null
            foreach ($prov in @('Microsoft.ACE.OLEDB.16.0','Microsoft.ACE.OLEDB.12.0','Microsoft.Jet.OLEDB.4.0')) {
              try {
                $c = New-Object System.Data.OleDb.OleDbConnection("Provider=$prov;Data Source=$mdb;")
                $c.Open()
                $conn = $c
                break
              } catch {}
            }
            if ($null -eq $conn) { throw 'No se encontró proveedor OLEDB Access (32-bit).' }
            $out = @{}
            $warns = New-Object System.Collections.ArrayList
            foreach ($t in $tables) {
              try {
                $cmd = $conn.CreateCommand()
                $cmd.CommandText = "SELECT * FROM [$t]"
                $da = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
                $dt = New-Object System.Data.DataTable
                [void]$da.Fill($dt)
                $list = New-Object System.Collections.ArrayList
                foreach ($r in $dt.Rows) {
                  $obj = @{}
                  foreach ($col in $dt.Columns) {
                    $v = $r[$col.ColumnName]
                    if ($v -is [DBNull]) { $obj[$col.ColumnName] = '' }
                    elseif ($v -is [DateTime]) { $obj[$col.ColumnName] = $v.ToString('o') }
                    else { $obj[$col.ColumnName] = [string]$v }
                  }
                  [void]$list.Add($obj)
                }
                $out[$t] = $list
              } catch {
                [void]$warns.Add("Tabla '$t' omitida: $($_.Exception.Message)")
              }
            }
            $conn.Close()
            @{ Tables = $out; Warnings = $warns } | ConvertTo-Json -Depth 6 -Compress
            """;

        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        ProcessStartInfo psi = new(ps86)
        {
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using Process proc = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar el lector x86.");
        string stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        string stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException($"Error leyendo Access MDB (x86): {stderr}");
        }

        using JsonDocument doc = JsonDocument.Parse(stdout);
        Dictionary<string, List<Dictionary<string, string>>> parsed = new(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.TryGetProperty("Warnings", out JsonElement warnEl) && warnEl.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement w in warnEl.EnumerateArray())
            {
                if (w.GetString() is string ws && !string.IsNullOrWhiteSpace(ws))
                {
                    warnings.Add(ws);
                }
            }
        }

        if (doc.RootElement.TryGetProperty("Tables", out JsonElement tblsEl) && tblsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty tblProp in tblsEl.EnumerateObject())
            {
                List<Dictionary<string, string>> rows = [];
                if (tblProp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement rowEl in tblProp.Value.EnumerateArray())
                    {
                        Dictionary<string, string> rowDict = new(StringComparer.OrdinalIgnoreCase);
                        foreach (JsonProperty colProp in rowEl.EnumerateObject())
                        {
                            rowDict[colProp.Name] = colProp.Value.ToString();
                        }
                        rows.Add(rowDict);
                    }
                }
                parsed[tblProp.Name] = rows;
            }
        }

        return parsed;
    }

    private static string GetStr(Dictionary<string, string> row, string key) =>
        row.TryGetValue(key, out string? val) ? val.Trim() : string.Empty;

    private static long ParseLong(string s) =>
        long.TryParse(s.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal),
            NumberStyles.Any, CultureInfo.InvariantCulture, out long v) ? v : 0;

    private static int ParseInt(string s) =>
        int.TryParse(s.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal),
            NumberStyles.Any, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static decimal ParseDecimal(string s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0m;

    private static DateOnly ParseDate(string s, DateOnly? fallback = null)
    {
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime iso))
        {
            return DateOnly.FromDateTime(iso);
        }

        if (DateTime.TryParse(s, new CultureInfo("es-CL"), DateTimeStyles.None, out DateTime cl))
        {
            return DateOnly.FromDateTime(cl);
        }

        return fallback ?? DateOnly.FromDateTime(DateTime.Today);
    }

    private static TimeOnly ParseTime(string s)
    {
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            return TimeOnly.FromDateTime(dt);
        }

        return TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out TimeOnly t)
            ? t
            : TimeOnly.MinValue;
    }
}
