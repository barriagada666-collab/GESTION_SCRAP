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
/// Migrador histórico desde Microsoft Access (.mdb/.accdb) con soporte automático para drivers x86 (SysWOW64).
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
        "PESAJE_PLACILLA"
    ];

    public async Task<(int PesajesImported, int ClientesImported, int ProductosImported, IReadOnlyList<string> Warnings)>
        ImportFromMdbAsync(string mdbPath, string usuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mdbPath) || !File.Exists(mdbPath))
        {
            throw new FileNotFoundException("No se encontró el archivo Microsoft Access especificado.", mdbPath);
        }

        List<string> warnings = [];
        Dictionary<string, List<Dictionary<string, object?>>> extracted;

        try
        {
            extracted = await ExtractWithNativeOleDbAsync(mdbPath, warnings, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            warnings.Add("Proveedor ACE/Jet de 64 bits no instalado; ejecutando extractor seguro vía Windows PowerShell 32 bits.");
            extracted = await ExtractViaWow64PowerShellAsync(mdbPath, warnings, ct).ConfigureAwait(false);
        }

        Dictionary<string, Producto> productos = new(StringComparer.OrdinalIgnoreCase);
        CollectProductos(extracted, "PRODUCTO_GRUPO", productos);
        CollectProductos(extracted, "PRODUCTO_GRUPO2", productos);
        CollectProductos(extracted, "PRODUCTO_GRUPO3", productos);
        CollectProductos(extracted, "PRODUCTO_GRUPOTHNO", productos);
        CollectProductos(extracted, "PRODUCTOS_CONS", productos);

        if (productos.Count > 0)
        {
            await _repository.UpsertProductosAsync(productos.Values, usuario, ct).ConfigureAwait(false);
        }

        Dictionary<string, Cliente> clientes = new(StringComparer.OrdinalIgnoreCase);
        CollectClientes(extracted, "CLIENTES", Planta.Santiago, clientes);
        CollectClientes(extracted, "CLIENTES_PLACILLA", Planta.Placilla, clientes);
        if (clientes.Count > 0)
        {
            await _repository.UpsertClientesAsync(clientes.Values, usuario, ct).ConfigureAwait(false);
        }

        IReadOnlyList<MaterialGroupNode> nodes = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        Dictionary<string, string> prodGroups = BusinessRules.BuildProductToRootGroupMap(nodes);
        foreach (Producto pr in productos.Values)
        {
            prodGroups.TryAdd(pr.Codigo, pr.Grupo);
        }

        int totalPesajes = 0;
        totalPesajes += await ImportPlantPesajesAsync(extracted, "PESAJE_PLACILLA_2", Planta.Placilla, prodGroups, usuario, ct).ConfigureAwait(false);
        totalPesajes += await ImportPlantPesajesAsync(extracted, "PESAJE_SANTIAGO", Planta.Santiago, prodGroups, usuario, ct).ConfigureAwait(false);
        totalPesajes += await ImportPlantPesajesAsync(extracted, "PESAJE_TALCAHUANO", Planta.Talcahuano, prodGroups, usuario, ct).ConfigureAwait(false);

        if (totalPesajes == 0 && extracted.ContainsKey("PESAJE_PLACILLA"))
        {
            totalPesajes += await ImportPlantPesajesAsync(extracted, "PESAJE_PLACILLA", Planta.Placilla, prodGroups, usuario, ct).ConfigureAwait(false);
        }

        await _repository.AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = usuario,
            Accion = "MIGRACION_MDB",
            Entidad = "ACCESS",
            Detalle = $"Migración desde '{Path.GetFileName(mdbPath)}': {totalPesajes} pesajes, {clientes.Count} clientes, {productos.Count} productos."
        }, ct).ConfigureAwait(false);

        return (totalPesajes, clientes.Count, productos.Count, warnings);
    }

    private async Task<int> ImportPlantPesajesAsync(
        Dictionary<string, List<Dictionary<string, object?>>> data,
        string tableName,
        Planta planta,
        Dictionary<string, string> groups,
        string usuario,
        CancellationToken ct)
    {
        if (!data.TryGetValue(tableName, out List<Dictionary<string, object?>>? rows) || rows.Count == 0)
        {
            return 0;
        }

        List<Pesaje> list = new(rows.Count);
        foreach (Dictionary<string, object?> row in rows)
        {
            long folio = GetLong(row, "FOLIO");
            if (folio <= 0)
            {
                continue;
            }

            string cod = GetStr(row, "COD_PRODUCTO");
            string nomProd = GetStr(row, "NOM_PRODUCTO");
            string tipoStr = GetStr(row, "TIPO_PESAJE");
            string rut = GetStr(row, "RUT_CLIENTE");
            string nomCli = GetStr(row, "NOM_CLIENTE");
            DateOnly fing = GetDate(row, "FECHA_INGRESO");
            DateOnly fsal = GetDate(row, "FECHA_SALIDA", fing);
            TimeOnly hing = GetTime(row, "HORA_INGRESO");
            TimeOnly hsal = GetTime(row, "HORA_SALIDA");
            int bruto = GetInt(row, "PESO_BRUTO");
            int tara = GetInt(row, "PESO_TARA");
            int neto = GetInt(row, "PESO_NETO");
            if (neto <= 0 && bruto > tara) neto = bruto - tara;

            string codeKey = cod.Trim().ToUpperInvariant();
            string grupo = groups.TryGetValue(codeKey, out string? gName)
                ? gName
                : BusinessRules.ClassifyProductoDefaultGroup(cod, nomProd);

            TipoPesaje tipo = TipoPesajeExtensions.FromLegacyValue(tipoStr);
            list.Add(new Pesaje
            {
                Folio = folio,
                Planta = planta,
                TipoPesaje = tipo,
                Patente = GetStr(row, "PATENTE"),
                PesoBruto = bruto,
                PesoTara = tara,
                PesoNeto = neto,
                CodProducto = codeKey,
                NomProducto = nomProd.ToUpperInvariant(),
                RutCliente = rut.ToUpperInvariant(),
                NomCliente = string.IsNullOrWhiteSpace(nomCli) ? rut : nomCli,
                Origen = GetStr(row, "ORIGEN"),
                Destino = GetStr(row, "DESTINO"),
                GuiaDespacho = GetStr(row, "GUIA_DESPACHO"),
                FechaIngreso = fing,
                HoraIngreso = hing,
                FechaSalida = fsal,
                HoraSalida = hsal,
                Observacion = GetStr(row, "OBSERVACION"),
                GrupoMaterial = grupo,
                EsTraslado = tipo != TipoPesaje.RecepcionCarga
            });
        }

        (int imported, _) = await _repository.ImportPesajesAsync(list, planta, usuario, ct).ConfigureAwait(false);
        return imported;
    }

    private static void CollectProductos(
        Dictionary<string, List<Dictionary<string, object?>>> data,
        string table,
        Dictionary<string, Producto> target)
    {
        if (!data.TryGetValue(table, out List<Dictionary<string, object?>>? rows))
        {
            return;
        }

        foreach (Dictionary<string, object?> row in rows)
        {
            string cod = GetStr(row, "CODIGO", "PRD_CODIGO", "COD_PRODUCTO").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(cod)) continue;

            string desc = GetStr(row, "DESCRIPCION", "PRD_DESCRIPCION", "NOM_PRODUCTO", "PRODUCTO").Trim();
            string grupoRaw = GetStr(row, "GRUPO", "PRD_FAMILIA").Trim();
            string grupo = !string.IsNullOrWhiteSpace(grupoRaw)
                ? grupoRaw.ToUpperInvariant()
                : BusinessRules.ClassifyProductoDefaultGroup(cod, desc);

            target[cod] = new Producto
            {
                Codigo = cod,
                Descripcion = string.IsNullOrWhiteSpace(desc) ? cod : desc.ToUpperInvariant(),
                Grupo = grupo,
                UnidadMedida = "KG"
            };
        }
    }

    private static void CollectClientes(
        Dictionary<string, List<Dictionary<string, object?>>> data,
        string table,
        Planta planta,
        Dictionary<string, Cliente> target)
    {
        if (!data.TryGetValue(table, out List<Dictionary<string, object?>>? rows))
        {
            return;
        }

        foreach (Dictionary<string, object?> row in rows)
        {
            string rut = GetStr(row, "RUT", "CPV_RUT").Trim();
            if (string.IsNullOrEmpty(rut)) continue;

            string dig = GetStr(row, "DIGITO").Trim();
            string nombre = GetStr(row, "NOMBRE", "CPV_RAZONSOCIAL").Trim();
            string ciudad = GetStr(row, "CIUDAD", "CPV_CIUDAD").Trim();
            string dir = GetStr(row, "DIRECCION", "CPV_DIRECCION").Trim();

            target[rut] = new Cliente
            {
                Rut = rut,
                Digito = dig,
                Nombre = string.IsNullOrEmpty(nombre) ? rut : nombre,
                RazonSocial = string.IsNullOrEmpty(nombre) ? rut : nombre,
                Ciudad = ciudad,
                Direccion = dir,
                PlantaAsociada = planta
            };
        }
    }

    private static async Task<Dictionary<string, List<Dictionary<string, object?>>>> ExtractWithNativeOleDbAsync(
        string mdbPath,
        List<string> warnings,
        CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("OleDb solo disponible en Windows.");
        }

        string[] providers = ["Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0", "Microsoft.Jet.OLEDB.4.0"];
        OleDbConnection? openConn = null;
        foreach (string prov in providers)
        {
            try
            {
                OleDbConnection candidate = new($"Provider={prov};Data Source={mdbPath};");
                await candidate.OpenAsync(ct).ConfigureAwait(false);
                openConn = candidate;
                break;
            }
            catch
            {
                // Prueba siguiente proveedor OLEDB
            }
        }

        if (openConn is null)
        {
            throw new InvalidOperationException("No hay proveedores OleDb de 64 bits disponibles.");
        }

        await using (openConn)
        {
            DataTable schema = openConn.GetSchema("Tables");
            HashSet<string> tableNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in schema.Rows)
            {
                string? name = r["TABLE_NAME"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    tableNames.Add(name);
                }
            }

            Dictionary<string, List<Dictionary<string, object?>>> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (string table in AllowedTables)
            {
                if (!tableNames.Contains(table)) continue;
                try
                {
                    await using OleDbCommand cmd = new($"SELECT * FROM [{table}]", openConn);
                    await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    List<Dictionary<string, object?>> rows = [];
                    while (await rd.ReadAsync(ct).ConfigureAwait(false))
                    {
                        Dictionary<string, object?> dict = new(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < rd.FieldCount; i++)
                        {
                            object v = rd.GetValue(i);
                            dict[rd.GetName(i)] = v == DBNull.Value ? null : v;
                        }
                        rows.Add(dict);
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

    private static async Task<Dictionary<string, List<Dictionary<string, object?>>>> ExtractViaWow64PowerShellAsync(
        string mdbPath,
        List<string> warnings,
        CancellationToken ct)
    {
        string ps86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"SysWOW64\WindowsPowerShell\v1.0\powershell.exe");

        if (!File.Exists(ps86))
        {
            throw new FileNotFoundException("No se encontró Windows PowerShell x86 en SysWOW64 para leer archivos Access de 32 bits.");
        }

        string tempJson = Path.Combine(Path.GetTempPath(), $"scrap_mdb_{Guid.NewGuid():N}.json");
        string tablesLiteral = string.Join(",", AllowedTables.Select(t => $"'{t}'"));
        string escapedMdb = mdbPath.Replace("'", "''", StringComparison.Ordinal);
        string escapedOut = tempJson.Replace("'", "''", StringComparison.Ordinal);

        string script = $$"""
            $ErrorActionPreference = 'Stop'
            $mdb = '{{escapedMdb}}'
            $outFile = '{{escapedOut}}'
            $tables = @({{tablesLiteral}})
            $conn = $null
            foreach ($prov in @('Microsoft.ACE.OLEDB.16.0','Microsoft.ACE.OLEDB.12.0','Microsoft.Jet.OLEDB.4.0')) {
                try {
                    $c = New-Object System.Data.OleDb.OleDbConnection("Provider=$prov;Data Source=$mdb;")
                    $c.Open()
                    $conn = $c
                    break
                } catch {}
            }
            if ($null -eq $conn) { throw 'No se encontro proveedor ACE/Jet x86 en el sistema.' }
            $schema = $conn.GetSchema('Tables')
            $existing = @{}
            foreach ($r in $schema.Rows) { $existing[$r['TABLE_NAME'].ToString().ToUpperInvariant()] = $true }
            $payload = @{}
            $warns = New-Object System.Collections.ArrayList
            foreach ($t in $tables) {
                if (-not $existing.ContainsKey($t.ToUpperInvariant())) { continue }
                try {
                    $cmd = $conn.CreateCommand()
                    $cmd.CommandText = "SELECT * FROM [$t]"
                    $rd = $cmd.ExecuteReader()
                    $list = New-Object System.Collections.ArrayList
                    while ($rd.Read()) {
                        $row = [ordered]@{}
                        for ($i = 0; $i -lt $rd.FieldCount; $i++) {
                            $v = $rd.GetValue($i)
                            if ($v -is [DBNull]) { $v = $null }
                            elseif ($v -is [DateTime]) { $v = $v.ToString('yyyy-MM-ddTHH:mm:ss') }
                            $row[$rd.GetName($i)] = $v
                        }
                        [void]$list.Add($row)
                    }
                    $rd.Close()
                    $payload[$t] = $list
                } catch {
                    [void]$warns.Add("Tabla '$t' omitida: $($_.Exception.Message)")
                }
            }
            $conn.Close()
            @{ Data = $payload; Warnings = $warns } | ConvertTo-Json -Depth 6 | Set-Content -Path $outFile -Encoding UTF8
            """;

        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = ps86,
                Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using Process? proc = Process.Start(psi) ?? throw new InvalidOperationException("No fue posible iniciar extractor Access x86.");
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            string err = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

            if (proc.ExitCode != 0 || !File.Exists(tempJson))
            {
                throw new InvalidOperationException($"Error en extracción Access x86: {err}");
            }

            string jsonText = await File.ReadAllTextAsync(tempJson, Encoding.UTF8, ct).ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(jsonText);

            if (doc.RootElement.TryGetProperty("Warnings", out JsonElement warnArr) && warnArr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement w in warnArr.EnumerateArray())
                {
                    if (w.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(w.GetString()))
                    {
                        warnings.Add(w.GetString()!);
                    }
                }
            }

            Dictionary<string, List<Dictionary<string, object?>>> result = new(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("Data", out JsonElement dataObj) && dataObj.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty tblProp in dataObj.EnumerateObject())
                {
                    List<Dictionary<string, object?>> list = [];
                    if (tblProp.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement rowEl in tblProp.Value.EnumerateArray())
                        {
                            Dictionary<string, object?> dict = new(StringComparer.OrdinalIgnoreCase);
                            foreach (JsonProperty col in rowEl.EnumerateObject())
                            {
                                dict[col.Name] = col.Value.ValueKind switch
                                {
                                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                                    JsonValueKind.String => col.Value.GetString(),
                                    JsonValueKind.Number => col.Value.TryGetInt64(out long lv) ? lv : col.Value.GetDecimal(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    _ => col.Value.ToString()
                                };
                            }
                            list.Add(dict);
                        }
                    }
                    result[tblProp.Name] = list;
                }
            }

            return result;
        }
        finally
        {
            if (File.Exists(tempJson))
            {
                File.Delete(tempJson);
            }
        }
    }

    private static string GetStr(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (string k in keys)
        {
            if (row.TryGetValue(k, out object? val) && val is not null)
            {
                return val.ToString()?.Trim() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static long GetLong(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out object? val) || val is null) return 0;
        return long.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out long res) ? res : 0;
    }

    private static int GetInt(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out object? val) || val is null) return 0;
        return decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? (int)Math.Round(d) : 0;
    }

    private static DateOnly GetDate(Dictionary<string, object?> row, string key, DateOnly? fallback = null)
    {
        if (row.TryGetValue(key, out object? val) && val is not null)
        {
            if (val is DateTime dt) return DateOnly.FromDateTime(dt);
            if (DateTime.TryParse(val.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return DateOnly.FromDateTime(parsed);
            }
        }
        return fallback ?? DateOnly.FromDateTime(DateTime.Today);
    }

    private static TimeOnly GetTime(Dictionary<string, object?> row, string key)
    {
        if (row.TryGetValue(key, out object? val) && val is not null)
        {
            if (val is DateTime dt) return TimeOnly.FromDateTime(dt);
            if (TimeOnly.TryParse(val.ToString(), CultureInfo.InvariantCulture, out TimeOnly parsed))
            {
                return parsed;
            }
        }
        return new TimeOnly(8, 0);
    }
}
