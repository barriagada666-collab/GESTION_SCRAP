using System.Globalization;
using System.Text.Json;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace GestionScrap.Infrastructure.Persistence;

public sealed partial class SqliteScrapRepository
{
    private static readonly (string Codigo, string Nombre, TipoPesaje TipoBase, bool DosMediciones, bool AfectaInv, int DirInv, int Orden)[] DefaultTiposPesaje =
    [
        ("RECEPCION", "Recepción de Carga", TipoPesaje.RecepcionCarga, false, true, 1, 1),
        ("DESPACHO", "Despacho de Carga", TipoPesaje.DespachoCarga, false, true, -1, 2),
        ("INTERNO", "Movimiento Interno", TipoPesaje.MovimientoInterno, false, false, 0, 3),
        ("ENTRADA", "Pesaje de Entrada", TipoPesaje.Entrada, false, true, 1, 4),
        ("SALIDA", "Pesaje de Salida", TipoPesaje.Salida, false, true, -1, 5),
        ("CARGA", "Pesaje de Carga (2 Pasos)", TipoPesaje.Carga, true, true, -1, 6),
        ("DESCARGA", "Pesaje de Descarga (2 Pasos)", TipoPesaje.Descarga, true, true, 1, 7),
        ("INDIVIDUAL", "Pesaje Individual (Control)", TipoPesaje.Individual, false, false, 0, 8),
        ("ENTRADA_SALIDA", "Entrada y Salida (2 Mediciones)", TipoPesaje.EntradaSalida, true, true, 1, 9)
    ];

    private static async Task ApplyMigrationV3EnterpriseTxAsync(SqliteConnection conn, SqliteTransaction tx, CancellationToken ct)
    {
        // 1. Extensión CRM en tabla clientes (sin pérdida de registros existentes)
        await EnsureColumnExistsAsync(conn, tx, "clientes", "rut_normalizado", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "nombre_comercial", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "contacto_nombre", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "email", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "condicion_comercial", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "observaciones", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "es_cliente", "INTEGER NOT NULL DEFAULT 1", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "es_proveedor", "INTEGER NOT NULL DEFAULT 1", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "activo", "INTEGER NOT NULL DEFAULT 1", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "clientes", "actualizado_en_utc", "TEXT", ct).ConfigureAwait(false);

        // 2. Extensión de catálogo en tabla productos
        await EnsureColumnExistsAsync(conn, tx, "productos", "nombre", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "productos", "informacion_adicional", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "productos", "activo", "INTEGER NOT NULL DEFAULT 1", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "productos", "tolerancia_diferencia_pct", "REAL NOT NULL DEFAULT 5.0", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "productos", "actualizado_en_utc", "TEXT", ct).ConfigureAwait(false);

        // 3. Extensión ERP / 2 Mediciones en tabla pesajes
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "folio_codigo", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "unidad_medida", "TEXT NOT NULL DEFAULT 'KG'", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "bascula", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "operador", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "estado_pesaje", "INTEGER NOT NULL DEFAULT 2", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "tipo_pesaje_config_id", "INTEGER", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "operacion_id", "INTEGER", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "vehiculo_id", "INTEGER", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "primera_medicion_kg", "INTEGER", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "primera_medicion_utc", "TEXT", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "segunda_medicion_kg", "INTEGER", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "segunda_medicion_utc", "TEXT", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "peso_esperado_kg", "INTEGER", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "ajuste_manual_autorizado", "INTEGER NOT NULL DEFAULT 0", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "motivo_ajuste_anulacion", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "modificado_por", "TEXT NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await EnsureColumnExistsAsync(conn, tx, "pesajes", "modificado_en_utc", "TEXT", ct).ConfigureAwait(false);

        // 4. Nuevas tablas empresariales CRM/ERP/Flota/Inventario/Auditoría de pesajes
        const string enterpriseDdl = """
            CREATE TABLE IF NOT EXISTS vehiculos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                patente_original TEXT NOT NULL,
                patente_normalizada TEXT NOT NULL UNIQUE,
                tipo_vehiculo TEXT NOT NULL DEFAULT 'Camión',
                marca TEXT NOT NULL DEFAULT '',
                modelo TEXT NOT NULL DEFAULT '',
                capacidad_carga_kg INTEGER NOT NULL DEFAULT 0,
                rut_entidad_asociada TEXT NOT NULL DEFAULT '',
                nombre_entidad_asociada TEXT NOT NULL DEFAULT '',
                activo INTEGER NOT NULL DEFAULT 1,
                observaciones TEXT NOT NULL DEFAULT '',
                creado_en_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS tipos_pesaje_config (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                codigo TEXT NOT NULL UNIQUE,
                nombre TEXT NOT NULL,
                tipo_base INTEGER NOT NULL,
                requiere_dos_mediciones INTEGER NOT NULL DEFAULT 0,
                afecta_inventario INTEGER NOT NULL DEFAULT 1,
                direccion_inventario INTEGER NOT NULL DEFAULT 1,
                activo INTEGER NOT NULL DEFAULT 1,
                orden INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS operaciones_comerciales (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                codigo TEXT NOT NULL UNIQUE,
                tipo_operacion INTEGER NOT NULL,
                fecha TEXT NOT NULL,
                rut_cliente TEXT NOT NULL DEFAULT '',
                nombre_cliente TEXT NOT NULL DEFAULT '',
                rut_proveedor TEXT NOT NULL DEFAULT '',
                nombre_proveedor TEXT NOT NULL DEFAULT '',
                cod_producto TEXT NOT NULL DEFAULT '',
                nombre_producto TEXT NOT NULL DEFAULT '',
                cantidad_esperada_kg INTEGER NOT NULL DEFAULT 0,
                cantidad_real_kg INTEGER NOT NULL DEFAULT 0,
                unidad_medida TEXT NOT NULL DEFAULT 'KG',
                patente TEXT NOT NULL DEFAULT '',
                documento_asociado TEXT NOT NULL DEFAULT '',
                estado INTEGER NOT NULL DEFAULT 1,
                observaciones TEXT NOT NULL DEFAULT '',
                creado_por TEXT NOT NULL DEFAULT '',
                creado_en_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS inventario_movimientos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_utc TEXT NOT NULL,
                tipo_movimiento INTEGER NOT NULL,
                cod_producto TEXT NOT NULL,
                nombre_producto TEXT NOT NULL DEFAULT '',
                cantidad_kg INTEGER NOT NULL,
                unidad_medida TEXT NOT NULL DEFAULT 'KG',
                pesaje_id INTEGER NULL REFERENCES pesajes(id) ON DELETE RESTRICT,
                folio_pesaje TEXT NOT NULL DEFAULT '',
                operacion_id INTEGER NULL REFERENCES operaciones_comerciales(id) ON DELETE SET NULL,
                codigo_operacion TEXT NOT NULL DEFAULT '',
                documento_referencia TEXT NOT NULL DEFAULT '',
                usuario TEXT NOT NULL DEFAULT '',
                observaciones TEXT NOT NULL DEFAULT '',
                anulado INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS pesajes_historial (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                pesaje_id INTEGER NOT NULL REFERENCES pesajes(id) ON DELETE CASCADE,
                timestamp_utc TEXT NOT NULL,
                usuario TEXT NOT NULL,
                accion TEXT NOT NULL,
                peso_bruto_anterior INTEGER NOT NULL,
                peso_tara_anterior INTEGER NOT NULL,
                peso_neto_anterior INTEGER NOT NULL,
                peso_bruto_nuevo INTEGER NOT NULL,
                peso_tara_nuevo INTEGER NOT NULL,
                peso_neto_nuevo INTEGER NOT NULL,
                estado_anterior INTEGER NOT NULL,
                estado_nuevo INTEGER NOT NULL,
                motivo TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS entidades_documentos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                rut_entidad TEXT NOT NULL DEFAULT '',
                operacion_id INTEGER NULL REFERENCES operaciones_comerciales(id) ON DELETE SET NULL,
                pesaje_id INTEGER NULL REFERENCES pesajes(id) ON DELETE SET NULL,
                tipo_documento TEXT NOT NULL,
                numero_documento TEXT NOT NULL,
                fecha_documento TEXT NOT NULL,
                observaciones TEXT NOT NULL DEFAULT '',
                creado_en_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_clientes_rut_norm ON clientes(rut_normalizado);
            CREATE INDEX IF NOT EXISTS idx_pesajes_patente ON pesajes(patente);
            CREATE INDEX IF NOT EXISTS idx_pesajes_estado ON pesajes(estado_pesaje);
            CREATE INDEX IF NOT EXISTS idx_pesajes_operacion ON pesajes(operacion_id);
            CREATE INDEX IF NOT EXISTS idx_pesajes_folio_codigo ON pesajes(folio_codigo);
            CREATE INDEX IF NOT EXISTS idx_vehiculos_rut ON vehiculos(rut_entidad_asociada);
            CREATE INDEX IF NOT EXISTS idx_operaciones_fecha_estado ON operaciones_comerciales(fecha DESC, estado);
            CREATE INDEX IF NOT EXISTS idx_operaciones_cod_prod ON operaciones_comerciales(cod_producto);
            CREATE INDEX IF NOT EXISTS idx_inv_mov_prod_ts ON inventario_movimientos(cod_producto, timestamp_utc DESC);
            CREATE INDEX IF NOT EXISTS idx_inv_mov_operacion ON inventario_movimientos(operacion_id);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_inv_mov_pesaje_activo
                ON inventario_movimientos(pesaje_id)
                WHERE pesaje_id IS NOT NULL AND tipo_movimiento != 7 AND anulado = 0;
            CREATE INDEX IF NOT EXISTS idx_pesajes_hist_pid ON pesajes_historial(pesaje_id, id DESC);
            CREATE INDEX IF NOT EXISTS idx_ent_docs_rut ON entidades_documentos(rut_entidad);
            CREATE INDEX IF NOT EXISTS idx_ent_docs_op ON entidades_documentos(operacion_id);
            """;

        await using (SqliteCommand ddlCmd = conn.CreateCommand())
        {
            ddlCmd.Transaction = tx;
            ddlCmd.CommandText = enterpriseDdl;
            await ddlCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // 5. Backfill no destructivo de columnas v5 sobre datos existentes
        await using (SqliteCommand bfProd = conn.CreateCommand())
        {
            bfProd.Transaction = tx;
            bfProd.CommandText = "UPDATE productos SET nombre = descripcion WHERE nombre = '';";
            await bfProd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using (SqliteCommand bfPes = conn.CreateCommand())
        {
            bfPes.Transaction = tx;
            bfPes.CommandText = "UPDATE pesajes SET folio_codigo = CAST(folio AS TEXT) WHERE folio_codigo = '';";
            await bfPes.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Normalizar RUT en clientes existentes
        List<(string Rut, string Digito, string Nombre)> existingClientes = [];
        await using (SqliteCommand selCli = conn.CreateCommand())
        {
            selCli.Transaction = tx;
            selCli.CommandText = "SELECT rut, digito, nombre FROM clientes WHERE rut_normalizado = '' OR nombre_comercial = ''";
            await using SqliteDataReader rd = await selCli.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                existingClientes.Add((rd.GetString(0), rd.GetString(1), rd.GetString(2)));
            }
        }

        foreach ((string rut, string digito, string nombre) in existingClientes)
        {
            string norm = RutChileno.NormalizeBody(string.IsNullOrWhiteSpace(digito) ? rut : $"{rut}-{digito}");
            await using SqliteCommand updCli = conn.CreateCommand();
            updCli.Transaction = tx;
            updCli.CommandText = """
                UPDATE clientes
                SET rut_normalizado = CASE WHEN rut_normalizado = '' THEN @rn ELSE rut_normalizado END,
                    nombre_comercial = CASE WHEN nombre_comercial = '' THEN @nc ELSE nombre_comercial END
                WHERE rut = @r
                """;
            updCli.Parameters.AddWithValue("@rn", norm);
            updCli.Parameters.AddWithValue("@nc", nombre);
            updCli.Parameters.AddWithValue("@r", rut);
            await updCli.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // 6. Seed de tipos de pesaje configurables
        foreach (var tp in DefaultTiposPesaje)
        {
            await using SqliteCommand insTp = conn.CreateCommand();
            insTp.Transaction = tx;
            insTp.CommandText = """
                INSERT OR IGNORE INTO tipos_pesaje_config(codigo, nombre, tipo_base, requiere_dos_mediciones, afecta_inventario, direccion_inventario, activo, orden)
                VALUES(@c, @n, @tb, @dos, @af, @dir, 1, @ord)
                """;
            insTp.Parameters.AddWithValue("@c", tp.Codigo);
            insTp.Parameters.AddWithValue("@n", tp.Nombre);
            insTp.Parameters.AddWithValue("@tb", (int)tp.TipoBase);
            insTp.Parameters.AddWithValue("@dos", tp.DosMediciones ? 1 : 0);
            insTp.Parameters.AddWithValue("@af", tp.AfectaInv ? 1 : 0);
            insTp.Parameters.AddWithValue("@dir", tp.DirInv);
            insTp.Parameters.AddWithValue("@ord", tp.Orden);
            await insTp.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // 7. Backfill inicial de flota desde patentes válidas históricas en pesajes
        List<(string Patente, string Rut, string Nombre)> existingPlates = [];
        await using (SqliteCommand selPat = conn.CreateCommand())
        {
            selPat.Transaction = tx;
            selPat.CommandText = """
                SELECT patente, MAX(rut_cliente), MAX(nom_cliente)
                FROM pesajes
                WHERE TRIM(patente) <> '' AND UPPER(TRIM(patente)) <> 'S/P'
                GROUP BY UPPER(TRIM(patente))
                """;
            await using SqliteDataReader rd = await selPat.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                existingPlates.Add((rd.GetString(0), rd.IsDBNull(1) ? string.Empty : rd.GetString(1), rd.IsDBNull(2) ? string.Empty : rd.GetString(2)));
            }
        }

        string nowUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        foreach ((string pat, string rut, string nom) in existingPlates)
        {
            if (!PatenteChilena.IsValidFormat(pat))
            {
                continue;
            }

            string norm = PatenteChilena.Normalize(pat);
            string display = PatenteChilena.FormatDisplay(norm);
            await using SqliteCommand insVeh = conn.CreateCommand();
            insVeh.Transaction = tx;
            insVeh.CommandText = """
                INSERT OR IGNORE INTO vehiculos(
                    patente_original, patente_normalizada, tipo_vehiculo, rut_entidad_asociada,
                    nombre_entidad_asociada, activo, observaciones, creado_en_utc)
                VALUES(@po, @pn, 'Camión', @rut, @nom, 1, 'Sincronizado automáticamente desde historial de pesajes.', @ts)
                """;
            insVeh.Parameters.AddWithValue("@po", display);
            insVeh.Parameters.AddWithValue("@pn", norm);
            insVeh.Parameters.AddWithValue("@rut", rut.Trim());
            insVeh.Parameters.AddWithValue("@nom", nom.Trim());
            insVeh.Parameters.AddWithValue("@ts", nowUtc);
            await insVeh.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // 8. Actualizar perfiles sembrados en bases existentes para incorporar permisos v5.0 (10..13)
        await SyncSeededProfilePermissionsTxAsync(conn, tx, ct).ConfigureAwait(false);
    }

    private static async Task EnsureColumnExistsAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken ct)
    {
        bool exists = false;
        await using (SqliteCommand checkCmd = conn.CreateCommand())
        {
            checkCmd.Transaction = tx;
            checkCmd.CommandText = $"PRAGMA table_info({tableName});";
            await using SqliteDataReader rd = await checkCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                if (string.Equals(rd.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            await using SqliteCommand alter = conn.CreateCommand();
            alter.Transaction = tx;
            alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task SyncSeededProfilePermissionsTxAsync(SqliteConnection conn, SqliteTransaction tx, CancellationToken ct)
    {
        IReadOnlyList<PerfilUsuario> defaults = DefaultRoleProfiles.GetSeededProfiles();
        foreach (PerfilUsuario seeded in defaults)
        {
            await using SqliteCommand sel = conn.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = seeded.EsSistema
                ? "SELECT id, permisos_json FROM role_profiles WHERE es_sistema = 1 LIMIT 1"
                : "SELECT id, permisos_json FROM role_profiles WHERE LOWER(nombre) = LOWER(@n) LIMIT 1";
            if (!seeded.EsSistema)
            {
                sel.Parameters.AddWithValue("@n", seeded.Nombre);
            }

            long? foundId = null;
            string existingJson = "[]";
            await using (SqliteDataReader rd = await sel.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                if (await rd.ReadAsync(ct).ConfigureAwait(false))
                {
                    foundId = rd.GetInt64(0);
                    existingJson = rd.GetString(1);
                }
            }

            if (!foundId.HasValue)
            {
                continue;
            }

            HashSet<PermisoSistema> merged = [.. (JsonSerializer.Deserialize<PermisoSistema[]>(existingJson) ?? [])];
            bool changed = false;
            foreach (PermisoSistema p in seeded.Permisos)
            {
                if (merged.Add(p))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                await using SqliteCommand upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE role_profiles SET permisos_json = @p WHERE id = @id";
                upd.Parameters.AddWithValue("@p", JsonSerializer.Serialize(merged.OrderBy(x => (int)x).ToArray()));
                upd.Parameters.AddWithValue("@id", foundId.Value);
                await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    private static Pesaje MapPesajeRow(SqliteDataReader rd) => new()
    {
        Id = rd.GetInt64(0),
        Folio = rd.GetInt64(1),
        Planta = (Planta)rd.GetInt32(2),
        TipoPesaje = (TipoPesaje)rd.GetInt32(3),
        Patente = rd.GetString(4),
        Acoplado = rd.GetString(5),
        PesoBruto = rd.GetInt32(6),
        PesoTara = rd.GetInt32(7),
        PesoNeto = rd.GetInt32(8),
        CodProducto = rd.GetString(9),
        NomProducto = rd.GetString(10),
        RutCliente = rd.GetString(11),
        NomCliente = rd.GetString(12),
        Origen = rd.GetString(13),
        Destino = rd.GetString(14),
        GuiaDespacho = rd.GetString(15),
        Chofer = rd.GetString(16),
        Transportista = rd.GetString(17),
        FechaIngreso = DateOnly.ParseExact(rd.GetString(18), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        HoraIngreso = TimeOnly.ParseExact(rd.GetString(19), "HH:mm:ss", CultureInfo.InvariantCulture),
        FechaSalida = DateOnly.ParseExact(rd.GetString(20), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        HoraSalida = TimeOnly.ParseExact(rd.GetString(21), "HH:mm:ss", CultureInfo.InvariantCulture),
        Observacion = rd.GetString(22),
        GrupoMaterial = rd.GetString(23),
        EsTraslado = rd.GetInt32(24) == 1,
        CiudadCliente = rd.IsDBNull(25) ? null : rd.GetString(25),
        FolioCodigo = rd.IsDBNull(26) ? string.Empty : rd.GetString(26),
        UnidadMedida = rd.IsDBNull(27) || string.IsNullOrWhiteSpace(rd.GetString(27)) ? "KG" : rd.GetString(27),
        Bascula = rd.IsDBNull(28) ? string.Empty : rd.GetString(28),
        Operador = rd.IsDBNull(29) ? string.Empty : rd.GetString(29),
        Estado = rd.IsDBNull(30) ? EstadoPesaje.Confirmado : (EstadoPesaje)rd.GetInt32(30),
        TipoPesajeConfigId = rd.IsDBNull(31) ? null : rd.GetInt64(31),
        OperacionId = rd.IsDBNull(32) ? null : rd.GetInt64(32),
        VehiculoId = rd.IsDBNull(33) ? null : rd.GetInt64(33),
        PrimeraMedicionKg = rd.IsDBNull(34) ? null : rd.GetInt32(34),
        PrimeraMedicionUtc = rd.IsDBNull(35) ? null : DateTime.Parse(rd.GetString(35), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        SegundaMedicionKg = rd.IsDBNull(36) ? null : rd.GetInt32(36),
        SegundaMedicionUtc = rd.IsDBNull(37) ? null : DateTime.Parse(rd.GetString(37), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        PesoEsperadoKg = rd.IsDBNull(38) ? null : rd.GetInt32(38),
        AjusteManualAutorizado = !rd.IsDBNull(39) && rd.GetInt32(39) == 1,
        MotivoAjusteOAnulacion = rd.IsDBNull(40) ? string.Empty : rd.GetString(40),
        ModificadoPor = rd.IsDBNull(41) ? string.Empty : rd.GetString(41),
        ModificadoEnUtc = rd.IsDBNull(42) ? null : DateTime.Parse(rd.GetString(42), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };

    private static Cliente MapClienteRow(SqliteDataReader rd) => new()
    {
        Rut = rd.GetString(0),
        Digito = rd.GetString(1),
        Nombre = rd.GetString(2),
        RazonSocial = rd.GetString(3),
        Giro = rd.GetString(4),
        Comuna = rd.GetString(5),
        Ciudad = rd.GetString(6),
        Direccion = rd.GetString(7),
        Telefono = rd.GetString(8),
        PlantaAsociada = rd.IsDBNull(9) ? null : (Planta)rd.GetInt32(9),
        RutNormalizado = rd.IsDBNull(10) ? string.Empty : rd.GetString(10),
        NombreComercial = rd.IsDBNull(11) ? string.Empty : rd.GetString(11),
        ContactoNombre = rd.IsDBNull(12) ? string.Empty : rd.GetString(12),
        Email = rd.IsDBNull(13) ? string.Empty : rd.GetString(13),
        CondicionComercial = rd.IsDBNull(14) ? string.Empty : rd.GetString(14),
        Observaciones = rd.IsDBNull(15) ? string.Empty : rd.GetString(15),
        EsCliente = rd.IsDBNull(16) || rd.GetInt32(16) == 1,
        EsProveedor = rd.IsDBNull(17) || rd.GetInt32(17) == 1,
        Activo = rd.IsDBNull(18) || rd.GetInt32(18) == 1,
        ActualizadoEnUtc = rd.IsDBNull(19) ? null : DateTime.Parse(rd.GetString(19), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };

    private static Producto MapProductoRow(SqliteDataReader rd) => new()
    {
        Codigo = rd.GetString(0),
        Descripcion = rd.GetString(1),
        Grupo = rd.GetString(2),
        UnidadMedida = rd.GetString(3),
        Nombre = rd.IsDBNull(4) ? string.Empty : rd.GetString(4),
        InformacionAdicional = rd.IsDBNull(5) ? string.Empty : rd.GetString(5),
        Activo = rd.IsDBNull(6) || rd.GetInt32(6) == 1,
        ToleranciaDiferenciaPct = rd.IsDBNull(7) ? 5.0 : rd.GetDouble(7),
        ActualizadoEnUtc = rd.IsDBNull(8) ? null : DateTime.Parse(rd.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };

    // =========================================================================
    // CRM ENTIDADES COMERCIALES UNIFICADAS (CLIENTES / PROVEEDORES)
    // =========================================================================

    public async Task<IReadOnlyList<Cliente>> GetEntidadesComercialesAsync(
        RolEntidadComercial? rol = null,
        bool? soloActivos = null,
        string? search = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();

        List<string> where = [];
        if (rol == RolEntidadComercial.Cliente)
        {
            where.Add("es_cliente = 1");
        }
        else if (rol == RolEntidadComercial.Proveedor)
        {
            where.Add("es_proveedor = 1");
        }
        else if (rol == RolEntidadComercial.Ambos)
        {
            where.Add("es_cliente = 1 AND es_proveedor = 1");
        }

        if (soloActivos.HasValue)
        {
            where.Add("activo = @act");
            cmd.Parameters.AddWithValue("@act", soloActivos.Value ? 1 : 0);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            string cleanTerm = RutChileno.Clean(term).TrimStart('0');
            where.Add("(LOWER(rut) LIKE @s OR LOWER(rut_normalizado) LIKE @sc OR LOWER(razon_social) LIKE @s OR LOWER(nombre) LIKE @s OR LOWER(nombre_comercial) LIKE @s OR LOWER(ciudad) LIKE @s OR LOWER(giro) LIKE @s)");
            cmd.Parameters.AddWithValue("@s", $"%{term}%");
            cmd.Parameters.AddWithValue("@sc", $"%{(string.IsNullOrEmpty(cleanTerm) ? term : cleanTerm.ToLowerInvariant())}%");
        }

        string clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = $"""
            SELECT rut, digito, nombre, razon_social, giro, comuna, ciudad, direccion, telefono, planta_asociada,
                   rut_normalizado, nombre_comercial, contacto_nombre, email, condicion_comercial,
                   observaciones, es_cliente, es_proveedor, activo, actualizado_en_utc
            FROM clientes
            {clause}
            ORDER BY activo DESC, razon_social ASC
            """;

        List<Cliente> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapClienteRow(rd));
        }

        return list;
    }

    public async Task<Cliente?> GetEntidadByRutAsync(string rut, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(rut))
        {
            return null;
        }

        string norm = RutChileno.NormalizeBody(rut);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT rut, digito, nombre, razon_social, giro, comuna, ciudad, direccion, telefono, planta_asociada,
                   rut_normalizado, nombre_comercial, contacto_nombre, email, condicion_comercial,
                   observaciones, es_cliente, es_proveedor, activo, actualizado_en_utc
            FROM clientes
            WHERE rut = @r OR (rut_normalizado <> '' AND rut_normalizado = @rn)
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@r", rut.Trim());
        cmd.Parameters.AddWithValue("@rn", norm);

        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false) ? MapClienteRow(rd) : null;
    }

    public async Task<Cliente> SaveEntidadComercialAsync(Cliente entidad, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string rutKey = entidad.Rut.Trim();
        string digito = entidad.Digito.Trim().ToUpperInvariant();
        string rutNorm = string.IsNullOrWhiteSpace(entidad.RutNormalizado)
            ? RutChileno.NormalizeBody(string.IsNullOrWhiteSpace(digito) ? rutKey : $"{rutKey}-{digito}")
            : entidad.RutNormalizado.Trim();

        if (string.IsNullOrWhiteSpace(rutKey) || string.IsNullOrWhiteSpace(rutNorm))
        {
            throw new ArgumentException("El RUT de la entidad comercial es obligatorio.");
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        // Verifica existencia previa por rut_normalizado para impedir duplicación del mismo RUT en CRM
        string? existingPk = null;
        await using (SqliteCommand chk = conn.CreateCommand())
        {
            chk.Transaction = tx;
            chk.CommandText = "SELECT rut FROM clientes WHERE rut = @r OR (rut_normalizado <> '' AND rut_normalizado = @rn) LIMIT 1";
            chk.Parameters.AddWithValue("@r", rutKey);
            chk.Parameters.AddWithValue("@rn", rutNorm);
            object? val = await chk.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (val is string s && !string.IsNullOrWhiteSpace(s))
            {
                existingPk = s;
            }
        }

        string effectivePk = existingPk ?? rutKey;
        string nowUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        string razon = string.IsNullOrWhiteSpace(entidad.RazonSocial) ? entidad.Nombre.Trim() : entidad.RazonSocial.Trim();
        string nombre = string.IsNullOrWhiteSpace(entidad.Nombre) ? razon : entidad.Nombre.Trim();
        string nombreCom = string.IsNullOrWhiteSpace(entidad.NombreComercial) ? nombre : entidad.NombreComercial.Trim();

        await using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO clientes(
                    rut, digito, nombre, razon_social, giro, comuna, ciudad, direccion, telefono, planta_asociada,
                    rut_normalizado, nombre_comercial, contacto_nombre, email, condicion_comercial,
                    observaciones, es_cliente, es_proveedor, activo, actualizado_en_utc)
                VALUES(
                    @r, @d, @n, @rs, @g, @com, @ci, @dir, @tel, @pl,
                    @rn, @nc, @cn, @em, @cc, @obs, @ec, @ep, @act, @ts)
                ON CONFLICT(rut) DO UPDATE SET
                    digito = excluded.digito,
                    nombre = excluded.nombre,
                    razon_social = excluded.razon_social,
                    giro = excluded.giro,
                    comuna = excluded.comuna,
                    ciudad = excluded.ciudad,
                    direccion = excluded.direccion,
                    telefono = excluded.telefono,
                    planta_asociada = excluded.planta_asociada,
                    rut_normalizado = excluded.rut_normalizado,
                    nombre_comercial = excluded.nombre_comercial,
                    contacto_nombre = excluded.contacto_nombre,
                    email = excluded.email,
                    condicion_comercial = excluded.condicion_comercial,
                    observaciones = excluded.observaciones,
                    es_cliente = excluded.es_cliente,
                    es_proveedor = excluded.es_proveedor,
                    activo = excluded.activo,
                    actualizado_en_utc = excluded.actualizado_en_utc
                """;
            cmd.Parameters.AddWithValue("@r", effectivePk);
            cmd.Parameters.AddWithValue("@d", digito);
            cmd.Parameters.AddWithValue("@n", nombre);
            cmd.Parameters.AddWithValue("@rs", razon);
            cmd.Parameters.AddWithValue("@g", entidad.Giro.Trim());
            cmd.Parameters.AddWithValue("@com", entidad.Comuna.Trim());
            cmd.Parameters.AddWithValue("@ci", entidad.Ciudad.Trim());
            cmd.Parameters.AddWithValue("@dir", entidad.Direccion.Trim());
            cmd.Parameters.AddWithValue("@tel", entidad.Telefono.Trim());
            cmd.Parameters.AddWithValue("@pl", entidad.PlantaAsociada.HasValue ? (int)entidad.PlantaAsociada.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@rn", rutNorm);
            cmd.Parameters.AddWithValue("@nc", nombreCom);
            cmd.Parameters.AddWithValue("@cn", entidad.ContactoNombre.Trim());
            cmd.Parameters.AddWithValue("@em", entidad.Email.Trim());
            cmd.Parameters.AddWithValue("@cc", entidad.CondicionComercial.Trim());
            cmd.Parameters.AddWithValue("@obs", entidad.Observaciones.Trim());
            cmd.Parameters.AddWithValue("@ec", entidad.EsCliente ? 1 : 0);
            cmd.Parameters.AddWithValue("@ep", entidad.EsProveedor ? 1 : 0);
            cmd.Parameters.AddWithValue("@act", entidad.Activo ? 1 : 0);
            cmd.Parameters.AddWithValue("@ts", nowUtc);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        string action = existingPk is null ? "CRM_ENTIDAD_CREAR" : "CRM_ENTIDAD_EDITAR";
        await AddAuditTxAsync(conn, tx, actor, action, "CRM", effectivePk,
            $"Entidad '{razon}' (RUT {effectivePk}-{digito}, Rol={entidad.RolDescripcion}, Activo={entidad.Activo}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new Cliente
        {
            Rut = effectivePk,
            Digito = digito,
            RutNormalizado = rutNorm,
            Nombre = nombre,
            RazonSocial = razon,
            NombreComercial = nombreCom,
            Giro = entidad.Giro.Trim(),
            Comuna = entidad.Comuna.Trim(),
            Ciudad = entidad.Ciudad.Trim(),
            Direccion = entidad.Direccion.Trim(),
            Telefono = entidad.Telefono.Trim(),
            ContactoNombre = entidad.ContactoNombre.Trim(),
            Email = entidad.Email.Trim(),
            CondicionComercial = entidad.CondicionComercial.Trim(),
            Observaciones = entidad.Observaciones.Trim(),
            EsCliente = entidad.EsCliente,
            EsProveedor = entidad.EsProveedor,
            Activo = entidad.Activo,
            PlantaAsociada = entidad.PlantaAsociada,
            ActualizadoEnUtc = DateTime.Parse(nowUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    public async Task SetEntidadActivoAsync(string rut, bool activo, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string norm = RutChileno.NormalizeBody(rut);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE clientes SET activo = @a, actualizado_en_utc = @ts WHERE rut = @r OR (rut_normalizado <> '' AND rut_normalizado = @rn)";
        cmd.Parameters.AddWithValue("@a", activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@r", rut.Trim());
        cmd.Parameters.AddWithValue("@rn", norm);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, activo ? "CRM_ENTIDAD_ACTIVAR" : "CRM_ENTIDAD_DESACTIVAR", "CRM", rut,
            $"Entidad RUT {rut} marcada como {(activo ? "Activa" : "Inactiva")}.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteEntidadSafelyAsync(string rut, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        Cliente? existing = await GetEntidadByRutAsync(rut, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return;
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        string norm = existing.RutClave;
        long pesajesRef = 0;
        await using (SqliteCommand chkPes = conn.CreateCommand())
        {
            chkPes.Transaction = tx;
            chkPes.CommandText = "SELECT COUNT(*) FROM pesajes WHERE rut_cliente = @r1 OR rut_cliente = @r2 OR REPLACE(REPLACE(rut_cliente, '.', ''), '-', '') LIKE @like";
            chkPes.Parameters.AddWithValue("@r1", existing.Rut);
            chkPes.Parameters.AddWithValue("@r2", existing.RutFormateado);
            chkPes.Parameters.AddWithValue("@like", $"{norm}%");
            pesajesRef = Convert.ToInt64(await chkPes.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        long opsRef = 0;
        await using (SqliteCommand chkOps = conn.CreateCommand())
        {
            chkOps.Transaction = tx;
            chkOps.CommandText = "SELECT COUNT(*) FROM operaciones_comerciales WHERE rut_cliente = @r1 OR rut_cliente = @r2 OR rut_proveedor = @r1 OR rut_proveedor = @r2";
            chkOps.Parameters.AddWithValue("@r1", existing.Rut);
            chkOps.Parameters.AddWithValue("@r2", existing.RutFormateado);
            opsRef = Convert.ToInt64(await chkOps.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        if (pesajesRef > 0 || opsRef > 0)
        {
            throw new InvalidOperationException(
                $"No se puede eliminar físicamente a '{existing.RazonSocial}' porque posee historial asociado ({pesajesRef} pesaje(s), {opsRef} operación(es)). Utilice la acción 'Desactivar' para conservar la integridad histórica.");
        }

        await using (SqliteCommand del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM clientes WHERE rut = @r";
            del.Parameters.AddWithValue("@r", existing.Rut);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, "CRM_ENTIDAD_ELIMINAR", "CRM", existing.Rut,
            $"Eliminada entidad sin movimientos '{existing.RazonSocial}' ({existing.RutFormateado}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    // =========================================================================
    // DOCUMENTOS ASOCIADOS
    // =========================================================================

    public async Task<IReadOnlyList<DocumentoAsociado>> GetDocumentosAsync(
        string? rutEntidad = null,
        long? operacionId = null,
        long? pesajeId = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();

        List<string> where = [];
        if (!string.IsNullOrWhiteSpace(rutEntidad))
        {
            where.Add("rut_entidad = @rut");
            cmd.Parameters.AddWithValue("@rut", RutChileno.NormalizeBody(rutEntidad));
        }

        if (operacionId.HasValue)
        {
            where.Add("operacion_id = @op");
            cmd.Parameters.AddWithValue("@op", operacionId.Value);
        }

        if (pesajeId.HasValue)
        {
            where.Add("pesaje_id = @pes");
            cmd.Parameters.AddWithValue("@pes", pesajeId.Value);
        }

        string clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = $"""
            SELECT id, rut_entidad, operacion_id, pesaje_id, tipo_documento, numero_documento, fecha_documento, observaciones, creado_en_utc
            FROM entidades_documentos
            {clause}
            ORDER BY fecha_documento DESC, id DESC
            """;

        List<DocumentoAsociado> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new DocumentoAsociado
            {
                Id = rd.GetInt64(0),
                RutEntidad = rd.GetString(1),
                OperacionId = rd.IsDBNull(2) ? null : rd.GetInt64(2),
                PesajeId = rd.IsDBNull(3) ? null : rd.GetInt64(3),
                TipoDocumento = rd.GetString(4),
                NumeroDocumento = rd.GetString(5),
                FechaDocumento = DateOnly.ParseExact(rd.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Observaciones = rd.GetString(7),
                CreadoEnUtc = DateTime.Parse(rd.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        return list;
    }

    public async Task<DocumentoAsociado> SaveDocumentoAsync(DocumentoAsociado doc, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(doc.NumeroDocumento))
        {
            throw new ArgumentException("El número de documento es obligatorio.");
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        string rutNorm = string.IsNullOrWhiteSpace(doc.RutEntidad) ? string.Empty : RutChileno.NormalizeBody(doc.RutEntidad);
        string ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO entidades_documentos(rut_entidad, operacion_id, pesaje_id, tipo_documento, numero_documento, fecha_documento, observaciones, creado_en_utc)
            VALUES(@rut, @op, @pes, @tipo, @num, @fec, @obs, @ts);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@rut", rutNorm);
        cmd.Parameters.AddWithValue("@op", (object?)doc.OperacionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pes", (object?)doc.PesajeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tipo", string.IsNullOrWhiteSpace(doc.TipoDocumento) ? "Guía de Despacho" : doc.TipoDocumento.Trim());
        cmd.Parameters.AddWithValue("@num", doc.NumeroDocumento.Trim());
        cmd.Parameters.AddWithValue("@fec", doc.FechaDocumento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@obs", doc.Observaciones.Trim());
        cmd.Parameters.AddWithValue("@ts", ts);
        long id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

        await AddAuditTxAsync(conn, tx, actor, "DOCUMENTO_ASOCIAR", "DOCUMENTOS", id.ToString(CultureInfo.InvariantCulture),
            $"{doc.TipoDocumento} #{doc.NumeroDocumento} asociado (RUT={rutNorm}, Op={doc.OperacionId}, Pesaje={doc.PesajeId}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new DocumentoAsociado
        {
            Id = id,
            RutEntidad = rutNorm,
            OperacionId = doc.OperacionId,
            PesajeId = doc.PesajeId,
            TipoDocumento = doc.TipoDocumento.Trim(),
            NumeroDocumento = doc.NumeroDocumento.Trim(),
            FechaDocumento = doc.FechaDocumento,
            Observaciones = doc.Observaciones.Trim(),
            CreadoEnUtc = DateTime.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    // =========================================================================
    // MATERIALES (PRODUCTOS EXTENDIDOS)
    // =========================================================================

    public async Task<IReadOnlyList<Producto>> GetMaterialesAsync(bool? soloActivos = null, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = soloActivos.HasValue
            ? """
              SELECT codigo, descripcion, grupo, unidad_medida, nombre, informacion_adicional, activo, tolerancia_diferencia_pct, actualizado_en_utc
              FROM productos
              WHERE activo = @a
              ORDER BY activo DESC, codigo ASC
              """
            : """
              SELECT codigo, descripcion, grupo, unidad_medida, nombre, informacion_adicional, activo, tolerancia_diferencia_pct, actualizado_en_utc
              FROM productos
              ORDER BY activo DESC, codigo ASC
              """;
        if (soloActivos.HasValue)
        {
            cmd.Parameters.AddWithValue("@a", soloActivos.Value ? 1 : 0);
        }

        List<Producto> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapProductoRow(rd));
        }

        return list;
    }

    public async Task<Producto?> GetMaterialByCodigoAsync(string codigo, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT codigo, descripcion, grupo, unidad_medida, nombre, informacion_adicional, activo, tolerancia_diferencia_pct, actualizado_en_utc
            FROM productos
            WHERE UPPER(codigo) = UPPER(@c)
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@c", codigo.Trim());
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false) ? MapProductoRow(rd) : null;
    }

    public async Task<Producto> SaveMaterialAsync(Producto material, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string code = material.Codigo.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("El código único de material es obligatorio.");
        }

        string nombre = string.IsNullOrWhiteSpace(material.Nombre)
            ? material.Descripcion.Trim().ToUpperInvariant()
            : material.Nombre.Trim().ToUpperInvariant();
        string desc = string.IsNullOrWhiteSpace(material.Descripcion)
            ? nombre
            : material.Descripcion.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(desc))
        {
            throw new ArgumentException("El nombre o descripción del material es obligatorio.");
        }

        string grupo = string.IsNullOrWhiteSpace(material.Grupo) ? "GENERAL" : material.Grupo.Trim().ToUpperInvariant();
        string unidad = string.IsNullOrWhiteSpace(material.UnidadMedida) ? "KG" : material.UnidadMedida.Trim().ToUpperInvariant();
        double tol = Math.Clamp(material.ToleranciaDiferenciaPct, 0.0, 100.0);
        string nowUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        bool existed;
        await using (SqliteCommand chk = conn.CreateCommand())
        {
            chk.Transaction = tx;
            chk.CommandText = "SELECT COUNT(*) FROM productos WHERE UPPER(codigo) = UPPER(@c)";
            chk.Parameters.AddWithValue("@c", code);
            existed = Convert.ToInt64(await chk.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture) > 0;
        }

        await using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO productos(codigo, descripcion, grupo, unidad_medida, nombre, informacion_adicional, activo, tolerancia_diferencia_pct, actualizado_en_utc)
                VALUES(@c, @d, @g, @u, @n, @info, @act, @tol, @ts)
                ON CONFLICT(codigo) DO UPDATE SET
                    descripcion = excluded.descripcion,
                    grupo = excluded.grupo,
                    unidad_medida = excluded.unidad_medida,
                    nombre = excluded.nombre,
                    informacion_adicional = excluded.informacion_adicional,
                    activo = excluded.activo,
                    tolerancia_diferencia_pct = excluded.tolerancia_diferencia_pct,
                    actualizado_en_utc = excluded.actualizado_en_utc
                """;
            cmd.Parameters.AddWithValue("@c", code);
            cmd.Parameters.AddWithValue("@d", desc);
            cmd.Parameters.AddWithValue("@g", grupo);
            cmd.Parameters.AddWithValue("@u", unidad);
            cmd.Parameters.AddWithValue("@n", nombre);
            cmd.Parameters.AddWithValue("@info", material.InformacionAdicional.Trim());
            cmd.Parameters.AddWithValue("@act", material.Activo ? 1 : 0);
            cmd.Parameters.AddWithValue("@tol", tol);
            cmd.Parameters.AddWithValue("@ts", nowUtc);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, existed ? "MATERIAL_EDITAR" : "MATERIAL_CREAR", "MATERIALES", code,
            $"Material '{code} · {nombre}' (Grupo={grupo}, Unidad={unidad}, Tolerancia={tol:0.##}%, Activo={material.Activo}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new Producto
        {
            Codigo = code,
            Nombre = nombre,
            Descripcion = desc,
            Grupo = grupo,
            UnidadMedida = unidad,
            InformacionAdicional = material.InformacionAdicional.Trim(),
            Activo = material.Activo,
            ToleranciaDiferenciaPct = tol,
            ActualizadoEnUtc = DateTime.Parse(nowUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    public async Task SetMaterialActivoAsync(string codigo, bool activo, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string code = codigo.Trim().ToUpperInvariant();
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE productos SET activo = @a, actualizado_en_utc = @ts WHERE UPPER(codigo) = UPPER(@c)";
        cmd.Parameters.AddWithValue("@a", activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@c", code);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, activo ? "MATERIAL_ACTIVAR" : "MATERIAL_DESACTIVAR", "MATERIALES", code,
            $"Material {code} marcado como {(activo ? "Activo" : "Inactivo")}.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteMaterialSafelyAsync(string codigo, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string code = codigo.Trim().ToUpperInvariant();
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        long pesRef = await CountScalarTxAsync(conn, tx, "SELECT COUNT(*) FROM pesajes WHERE UPPER(cod_producto) = UPPER(@c)", "@c", code, ct).ConfigureAwait(false);
        long invRef = await CountScalarTxAsync(conn, tx, "SELECT COUNT(*) FROM inventario_movimientos WHERE UPPER(cod_producto) = UPPER(@c)", "@c", code, ct).ConfigureAwait(false);
        long opsRef = await CountScalarTxAsync(conn, tx, "SELECT COUNT(*) FROM operaciones_comerciales WHERE UPPER(cod_producto) = UPPER(@c)", "@c", code, ct).ConfigureAwait(false);

        if (pesRef > 0 || invRef > 0 || opsRef > 0)
        {
            throw new InvalidOperationException(
                $"No se puede eliminar físicamente el material '{code}' porque tiene historial ({pesRef} pesaje(s), {invRef} movimiento(s) inventario, {opsRef} operación(es)). Utilice 'Desactivar' para proteger la trazabilidad.");
        }

        await using (SqliteCommand del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM productos WHERE UPPER(codigo) = UPPER(@c)";
            del.Parameters.AddWithValue("@c", code);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, "MATERIAL_ELIMINAR", "MATERIALES", code,
            $"Eliminado material sin historial '{code}'.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    // =========================================================================
    // VEHÍCULOS Y PATENTES
    // =========================================================================

    public async Task<IReadOnlyList<Vehiculo>> GetVehiculosAsync(bool? soloActivos = null, string? patenteSearch = null, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();

        List<string> where = [];
        if (soloActivos.HasValue)
        {
            where.Add("activo = @a");
            cmd.Parameters.AddWithValue("@a", soloActivos.Value ? 1 : 0);
        }

        if (!string.IsNullOrWhiteSpace(patenteSearch))
        {
            string normSearch = PatenteChilena.Normalize(patenteSearch);
            string rawSearch = patenteSearch.Trim().ToLowerInvariant();
            where.Add("(patente_normalizada LIKE @pn OR LOWER(patente_original) LIKE @raw OR LOWER(nombre_entidad_asociada) LIKE @raw OR LOWER(marca) LIKE @raw OR LOWER(tipo_vehiculo) LIKE @raw)");
            cmd.Parameters.AddWithValue("@pn", $"%{(string.IsNullOrEmpty(normSearch) ? rawSearch.ToUpperInvariant() : normSearch)}%");
            cmd.Parameters.AddWithValue("@raw", $"%{rawSearch}%");
        }

        string clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = $"""
            SELECT id, patente_original, patente_normalizada, tipo_vehiculo, marca, modelo, capacidad_carga_kg,
                   rut_entidad_asociada, nombre_entidad_asociada, activo, observaciones, creado_en_utc
            FROM vehiculos
            {clause}
            ORDER BY activo DESC, patente_normalizada ASC
            """;

        List<Vehiculo> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapVehiculoRow(rd));
        }

        return list;
    }

    public async Task<Vehiculo?> GetVehiculoByPatenteAsync(string patente, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string norm = PatenteChilena.Normalize(patente);
        if (string.IsNullOrEmpty(norm))
        {
            return null;
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, patente_original, patente_normalizada, tipo_vehiculo, marca, modelo, capacidad_carga_kg,
                   rut_entidad_asociada, nombre_entidad_asociada, activo, observaciones, creado_en_utc
            FROM vehiculos
            WHERE patente_normalizada = @pn
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@pn", norm);

        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false) ? MapVehiculoRow(rd) : null;
    }

    public async Task<Vehiculo> SaveVehiculoAsync(Vehiculo vehiculo, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string norm = PatenteChilena.Normalize(vehiculo.PatenteOriginal);
        if (!PatenteChilena.IsValidFormat(norm))
        {
            throw new ArgumentException("Debe ingresar una patente válida (entre 5 y 10 caracteres alfanuméricos).");
        }

        if (vehiculo.CapacidadCargaKg < 0)
        {
            throw new ArgumentException("La capacidad de carga no puede ser negativa.");
        }

        string display = PatenteChilena.FormatDisplay(norm);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        // Impedir duplicados normalizados (ej. AB-CD-12 y abcd12)
        await using (SqliteCommand dup = conn.CreateCommand())
        {
            dup.Transaction = tx;
            dup.CommandText = "SELECT id FROM vehiculos WHERE patente_normalizada = @pn AND id <> @id LIMIT 1";
            dup.Parameters.AddWithValue("@pn", norm);
            dup.Parameters.AddWithValue("@id", vehiculo.Id);
            object? clash = await dup.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (clash is not null)
            {
                throw new InvalidOperationException($"Ya existe un vehículo registrado con la patente normalizada '{display}' ({norm}).");
            }
        }

        long id = vehiculo.Id;
        string nowUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (id == 0)
        {
            await using SqliteCommand ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO vehiculos(
                    patente_original, patente_normalizada, tipo_vehiculo, marca, modelo, capacidad_carga_kg,
                    rut_entidad_asociada, nombre_entidad_asociada, activo, observaciones, creado_en_utc)
                VALUES(@po, @pn, @tv, @ma, @mo, @cap, @rut, @nom, @act, @obs, @ts);
                SELECT last_insert_rowid();
                """;
            ins.Parameters.AddWithValue("@po", display);
            ins.Parameters.AddWithValue("@pn", norm);
            ins.Parameters.AddWithValue("@tv", string.IsNullOrWhiteSpace(vehiculo.TipoVehiculo) ? "Camión" : vehiculo.TipoVehiculo.Trim());
            ins.Parameters.AddWithValue("@ma", vehiculo.Marca.Trim());
            ins.Parameters.AddWithValue("@mo", vehiculo.Modelo.Trim());
            ins.Parameters.AddWithValue("@cap", vehiculo.CapacidadCargaKg);
            ins.Parameters.AddWithValue("@rut", vehiculo.RutEntidadAsociada.Trim());
            ins.Parameters.AddWithValue("@nom", vehiculo.NombreEntidadAsociada.Trim());
            ins.Parameters.AddWithValue("@act", vehiculo.Activo ? 1 : 0);
            ins.Parameters.AddWithValue("@obs", vehiculo.Observaciones.Trim());
            ins.Parameters.AddWithValue("@ts", nowUtc);
            id = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
        else
        {
            await using SqliteCommand upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE vehiculos
                SET patente_original = @po, patente_normalizada = @pn, tipo_vehiculo = @tv,
                    marca = @ma, modelo = @mo, capacidad_carga_kg = @cap,
                    rut_entidad_asociada = @rut, nombre_entidad_asociada = @nom,
                    activo = @act, observaciones = @obs
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("@id", id);
            upd.Parameters.AddWithValue("@po", display);
            upd.Parameters.AddWithValue("@pn", norm);
            upd.Parameters.AddWithValue("@tv", string.IsNullOrWhiteSpace(vehiculo.TipoVehiculo) ? "Camión" : vehiculo.TipoVehiculo.Trim());
            upd.Parameters.AddWithValue("@ma", vehiculo.Marca.Trim());
            upd.Parameters.AddWithValue("@mo", vehiculo.Modelo.Trim());
            upd.Parameters.AddWithValue("@cap", vehiculo.CapacidadCargaKg);
            upd.Parameters.AddWithValue("@rut", vehiculo.RutEntidadAsociada.Trim());
            upd.Parameters.AddWithValue("@nom", vehiculo.NombreEntidadAsociada.Trim());
            upd.Parameters.AddWithValue("@act", vehiculo.Activo ? 1 : 0);
            upd.Parameters.AddWithValue("@obs", vehiculo.Observaciones.Trim());
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, vehiculo.Id == 0 ? "VEHICULO_CREAR" : "VEHICULO_EDITAR", "FLOTA",
            id.ToString(CultureInfo.InvariantCulture), $"Vehículo {display} ({vehiculo.TipoVehiculo}, Activo={vehiculo.Activo}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new Vehiculo
        {
            Id = id,
            PatenteOriginal = display,
            PatenteNormalizada = norm,
            TipoVehiculo = string.IsNullOrWhiteSpace(vehiculo.TipoVehiculo) ? "Camión" : vehiculo.TipoVehiculo.Trim(),
            Marca = vehiculo.Marca.Trim(),
            Modelo = vehiculo.Modelo.Trim(),
            CapacidadCargaKg = vehiculo.CapacidadCargaKg,
            RutEntidadAsociada = vehiculo.RutEntidadAsociada.Trim(),
            NombreEntidadAsociada = vehiculo.NombreEntidadAsociada.Trim(),
            Activo = vehiculo.Activo,
            Observaciones = vehiculo.Observaciones.Trim(),
            CreadoEnUtc = vehiculo.Id == 0 ? DateTime.Parse(nowUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : vehiculo.CreadoEnUtc
        };
    }

    public async Task SetVehiculoActivoAsync(long id, bool activo, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE vehiculos SET activo = @a WHERE id = @id";
        cmd.Parameters.AddWithValue("@a", activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, activo ? "VEHICULO_ACTIVAR" : "VEHICULO_DESACTIVAR", "FLOTA",
            id.ToString(CultureInfo.InvariantCulture), $"Vehículo #{id} marcado como {(activo ? "Activo" : "Inactivo")}.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteVehiculoSafelyAsync(long id, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        string? patNorm = null;
        await using (SqliteCommand sel = conn.CreateCommand())
        {
            sel.Transaction = tx;
            sel.CommandText = "SELECT patente_normalizada FROM vehiculos WHERE id = @id";
            sel.Parameters.AddWithValue("@id", id);
            patNorm = Convert.ToString(await sel.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        if (string.IsNullOrWhiteSpace(patNorm))
        {
            return;
        }

        long pesRef = 0;
        await using (SqliteCommand chk = conn.CreateCommand())
        {
            chk.Transaction = tx;
            chk.CommandText = "SELECT COUNT(*) FROM pesajes WHERE vehiculo_id = @id OR REPLACE(REPLACE(UPPER(patente), '-', ''), ' ', '') = @pn";
            chk.Parameters.AddWithValue("@id", id);
            chk.Parameters.AddWithValue("@pn", patNorm);
            pesRef = Convert.ToInt64(await chk.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        if (pesRef > 0)
        {
            throw new InvalidOperationException(
                $"No se puede eliminar físicamente el vehículo '{PatenteChilena.FormatDisplay(patNorm)}' porque posee {pesRef} pesaje(s) históricos. Utilice 'Desactivar' para conservar la trazabilidad.");
        }

        await using (SqliteCommand del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM vehiculos WHERE id = @id";
            del.Parameters.AddWithValue("@id", id);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, "VEHICULO_ELIMINAR", "FLOTA", id.ToString(CultureInfo.InvariantCulture),
            $"Eliminado vehículo sin historial ({PatenteChilena.FormatDisplay(patNorm)}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static Vehiculo MapVehiculoRow(SqliteDataReader rd) => new()
    {
        Id = rd.GetInt64(0),
        PatenteOriginal = rd.GetString(1),
        PatenteNormalizada = rd.GetString(2),
        TipoVehiculo = rd.GetString(3),
        Marca = rd.GetString(4),
        Modelo = rd.GetString(5),
        CapacidadCargaKg = rd.GetInt32(6),
        RutEntidadAsociada = rd.GetString(7),
        NombreEntidadAsociada = rd.GetString(8),
        Activo = rd.GetInt32(9) == 1,
        Observaciones = rd.GetString(10),
        CreadoEnUtc = DateTime.Parse(rd.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };

    // =========================================================================
    // CONFIGURACIÓN DE TIPOS DE PESAJE
    // =========================================================================

    public async Task<IReadOnlyList<TipoPesajeConfig>> GetTiposPesajeConfigAsync(bool? soloActivos = null, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = soloActivos.HasValue
            ? "SELECT id, codigo, nombre, tipo_base, requiere_dos_mediciones, afecta_inventario, direccion_inventario, activo, orden FROM tipos_pesaje_config WHERE activo = @a ORDER BY orden, nombre"
            : "SELECT id, codigo, nombre, tipo_base, requiere_dos_mediciones, afecta_inventario, direccion_inventario, activo, orden FROM tipos_pesaje_config ORDER BY orden, nombre";
        if (soloActivos.HasValue)
        {
            cmd.Parameters.AddWithValue("@a", soloActivos.Value ? 1 : 0);
        }

        List<TipoPesajeConfig> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new TipoPesajeConfig
            {
                Id = rd.GetInt64(0),
                Codigo = rd.GetString(1),
                Nombre = rd.GetString(2),
                TipoBase = (TipoPesaje)rd.GetInt32(3),
                RequiereDosMediciones = rd.GetInt32(4) == 1,
                AfectaInventario = rd.GetInt32(5) == 1,
                DireccionInventario = rd.GetInt32(6),
                Activo = rd.GetInt32(7) == 1,
                Orden = rd.GetInt32(8)
            });
        }

        return list;
    }

    public async Task<TipoPesajeConfig> SaveTipoPesajeConfigAsync(TipoPesajeConfig config, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string code = config.Codigo.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(config.Nombre))
        {
            throw new ArgumentException("El código y el nombre del tipo de pesaje son obligatorios.");
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        long id = config.Id;
        int dirInv = config.AfectaInventario ? Math.Sign(config.DireccionInventario) : 0;

        if (id == 0)
        {
            await using SqliteCommand ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO tipos_pesaje_config(codigo, nombre, tipo_base, requiere_dos_mediciones, afecta_inventario, direccion_inventario, activo, orden)
                VALUES(@c, @n, @tb, @dos, @af, @dir, @act, @ord);
                SELECT last_insert_rowid();
                """;
            ins.Parameters.AddWithValue("@c", code);
            ins.Parameters.AddWithValue("@n", config.Nombre.Trim());
            ins.Parameters.AddWithValue("@tb", (int)config.TipoBase);
            ins.Parameters.AddWithValue("@dos", config.RequiereDosMediciones ? 1 : 0);
            ins.Parameters.AddWithValue("@af", config.AfectaInventario ? 1 : 0);
            ins.Parameters.AddWithValue("@dir", dirInv);
            ins.Parameters.AddWithValue("@act", config.Activo ? 1 : 0);
            ins.Parameters.AddWithValue("@ord", config.Orden <= 0 ? 50 : config.Orden);
            id = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
        else
        {
            await using SqliteCommand upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE tipos_pesaje_config
                SET codigo = @c, nombre = @n, tipo_base = @tb, requiere_dos_mediciones = @dos,
                    afecta_inventario = @af, direccion_inventario = @dir, activo = @act, orden = @ord
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("@id", id);
            upd.Parameters.AddWithValue("@c", code);
            upd.Parameters.AddWithValue("@n", config.Nombre.Trim());
            upd.Parameters.AddWithValue("@tb", (int)config.TipoBase);
            upd.Parameters.AddWithValue("@dos", config.RequiereDosMediciones ? 1 : 0);
            upd.Parameters.AddWithValue("@af", config.AfectaInventario ? 1 : 0);
            upd.Parameters.AddWithValue("@dir", dirInv);
            upd.Parameters.AddWithValue("@act", config.Activo ? 1 : 0);
            upd.Parameters.AddWithValue("@ord", config.Orden);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, config.Id == 0 ? "TIPO_PESAJE_CREAR" : "TIPO_PESAJE_EDITAR", "CONFIG_PESAJES",
            code, $"Tipo de pesaje '{config.Nombre}' (2 pasos={config.RequiereDosMediciones}, Inv={dirInv}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new TipoPesajeConfig
        {
            Id = id,
            Codigo = code,
            Nombre = config.Nombre.Trim(),
            TipoBase = config.TipoBase,
            RequiereDosMediciones = config.RequiereDosMediciones,
            AfectaInventario = config.AfectaInventario,
            DireccionInventario = dirInv,
            Activo = config.Activo,
            Orden = config.Orden <= 0 ? 50 : config.Orden
        };
    }

    public async Task SetTipoPesajeConfigActivoAsync(long id, bool activo, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE tipos_pesaje_config SET activo = @a WHERE id = @id";
        cmd.Parameters.AddWithValue("@a", activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, activo ? "TIPO_PESAJE_ACTIVAR" : "TIPO_PESAJE_DESACTIVAR", "CONFIG_PESAJES",
            id.ToString(CultureInfo.InvariantCulture), $"Tipo de pesaje #{id} marcado como {(activo ? "Activo" : "Inactivo")}.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    // =========================================================================
    // OPERACIONES COMERCIALES ERP
    // =========================================================================

    public async Task<IReadOnlyList<OperacionComercial>> GetOperacionesAsync(
        TipoOperacionComercial? tipo = null,
        EstadoOperacion? estado = null,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();

        List<string> where = [];
        if (tipo.HasValue)
        {
            where.Add("tipo_operacion = @t");
            cmd.Parameters.AddWithValue("@t", (int)tipo.Value);
        }

        if (estado.HasValue)
        {
            where.Add("estado = @e");
            cmd.Parameters.AddWithValue("@e", (int)estado.Value);
        }

        if (desde.HasValue)
        {
            where.Add("fecha >= @d");
            cmd.Parameters.AddWithValue("@d", desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (hasta.HasValue)
        {
            where.Add("fecha <= @h");
            cmd.Parameters.AddWithValue("@h", hasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        string clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = $"""
            SELECT id, codigo, tipo_operacion, fecha, rut_cliente, nombre_cliente, rut_proveedor, nombre_proveedor,
                   cod_producto, nombre_producto, cantidad_esperada_kg, cantidad_real_kg, unidad_medida, patente,
                   documento_asociado, estado, observaciones, creado_por, creado_en_utc
            FROM operaciones_comerciales
            {clause}
            ORDER BY fecha DESC, id DESC
            """;

        List<OperacionComercial> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapOperacionRow(rd));
        }

        return list;
    }

    public async Task<OperacionComercial?> GetOperacionByIdAsync(long id, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, codigo, tipo_operacion, fecha, rut_cliente, nombre_cliente, rut_proveedor, nombre_proveedor,
                   cod_producto, nombre_producto, cantidad_esperada_kg, cantidad_real_kg, unidad_medida, patente,
                   documento_asociado, estado, observaciones, creado_por, creado_en_utc
            FROM operaciones_comerciales
            WHERE id = @id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@id", id);

        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false) ? MapOperacionRow(rd) : null;
    }

    public async Task<OperacionComercial> SaveOperacionAsync(OperacionComercial op, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        if (op.CantidadEsperadaKg < 0)
        {
            throw new ArgumentException("La cantidad esperada no puede ser negativa.");
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        string code = string.IsNullOrWhiteSpace(op.Codigo)
            ? $"OP-{op.Fecha:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}"
            : op.Codigo.Trim().ToUpperInvariant();
        string patFormatted = string.IsNullOrWhiteSpace(op.Patente)
            ? string.Empty
            : PatenteChilena.FormatDisplay(op.Patente);
        string unidad = string.IsNullOrWhiteSpace(op.UnidadMedida) ? "KG" : op.UnidadMedida.Trim().ToUpperInvariant();
        string nowUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        long id = op.Id;
        if (id == 0)
        {
            await using SqliteCommand ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO operaciones_comerciales(
                    codigo, tipo_operacion, fecha, rut_cliente, nombre_cliente, rut_proveedor, nombre_proveedor,
                    cod_producto, nombre_producto, cantidad_esperada_kg, cantidad_real_kg, unidad_medida, patente,
                    documento_asociado, estado, observaciones, creado_por, creado_en_utc)
                VALUES(@c, @to, @f, @rc, @nc, @rp, @np, @cp, @npr, @ce, @cr, @um, @pat, @doc, @est, @obs, @usr, @ts);
                SELECT last_insert_rowid();
                """;
            ins.Parameters.AddWithValue("@c", code);
            ins.Parameters.AddWithValue("@to", (int)op.TipoOperacion);
            ins.Parameters.AddWithValue("@f", op.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            ins.Parameters.AddWithValue("@rc", op.RutCliente.Trim());
            ins.Parameters.AddWithValue("@nc", op.NombreCliente.Trim());
            ins.Parameters.AddWithValue("@rp", op.RutProveedor.Trim());
            ins.Parameters.AddWithValue("@np", op.NombreProveedor.Trim());
            ins.Parameters.AddWithValue("@cp", op.CodProducto.Trim().ToUpperInvariant());
            ins.Parameters.AddWithValue("@npr", op.NombreProducto.Trim());
            ins.Parameters.AddWithValue("@ce", op.CantidadEsperadaKg);
            ins.Parameters.AddWithValue("@cr", op.CantidadRealConfirmadaKg);
            ins.Parameters.AddWithValue("@um", unidad);
            ins.Parameters.AddWithValue("@pat", patFormatted);
            ins.Parameters.AddWithValue("@doc", op.DocumentoAsociado.Trim());
            ins.Parameters.AddWithValue("@est", (int)op.Estado);
            ins.Parameters.AddWithValue("@obs", op.Observaciones.Trim());
            ins.Parameters.AddWithValue("@usr", string.IsNullOrWhiteSpace(op.CreadoPor) ? actor : op.CreadoPor.Trim());
            ins.Parameters.AddWithValue("@ts", nowUtc);
            id = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
        else
        {
            await using SqliteCommand upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE operaciones_comerciales
                SET codigo = @c, tipo_operacion = @to, fecha = @f, rut_cliente = @rc, nombre_cliente = @nc,
                    rut_proveedor = @rp, nombre_proveedor = @np, cod_producto = @cp, nombre_producto = @npr,
                    cantidad_esperada_kg = @ce, cantidad_real_kg = @cr, unidad_medida = @um, patente = @pat,
                    documento_asociado = @doc, estado = @est, observaciones = @obs
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("@id", id);
            upd.Parameters.AddWithValue("@c", code);
            upd.Parameters.AddWithValue("@to", (int)op.TipoOperacion);
            upd.Parameters.AddWithValue("@f", op.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            upd.Parameters.AddWithValue("@rc", op.RutCliente.Trim());
            upd.Parameters.AddWithValue("@nc", op.NombreCliente.Trim());
            upd.Parameters.AddWithValue("@rp", op.RutProveedor.Trim());
            upd.Parameters.AddWithValue("@np", op.NombreProveedor.Trim());
            upd.Parameters.AddWithValue("@cp", op.CodProducto.Trim().ToUpperInvariant());
            upd.Parameters.AddWithValue("@npr", op.NombreProducto.Trim());
            upd.Parameters.AddWithValue("@ce", op.CantidadEsperadaKg);
            upd.Parameters.AddWithValue("@cr", op.CantidadRealConfirmadaKg);
            upd.Parameters.AddWithValue("@um", unidad);
            upd.Parameters.AddWithValue("@pat", patFormatted);
            upd.Parameters.AddWithValue("@doc", op.DocumentoAsociado.Trim());
            upd.Parameters.AddWithValue("@est", (int)op.Estado);
            upd.Parameters.AddWithValue("@obs", op.Observaciones.Trim());
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, op.Id == 0 ? "OPERACION_CREAR" : "OPERACION_EDITAR", "ERP_OPERACIONES",
            code, $"{op.TipoDescripcion} '{code}' (Estado={op.EstadoDescripcion}, Esperado={op.CantidadEsperadaKg:N0} {unidad}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new OperacionComercial
        {
            Id = id,
            Codigo = code,
            TipoOperacion = op.TipoOperacion,
            Fecha = op.Fecha,
            RutCliente = op.RutCliente.Trim(),
            NombreCliente = op.NombreCliente.Trim(),
            RutProveedor = op.RutProveedor.Trim(),
            NombreProveedor = op.NombreProveedor.Trim(),
            CodProducto = op.CodProducto.Trim().ToUpperInvariant(),
            NombreProducto = op.NombreProducto.Trim(),
            CantidadEsperadaKg = op.CantidadEsperadaKg,
            CantidadRealConfirmadaKg = op.CantidadRealConfirmadaKg,
            UnidadMedida = unidad,
            Patente = patFormatted,
            DocumentoAsociado = op.DocumentoAsociado.Trim(),
            Estado = op.Estado,
            Observaciones = op.Observaciones.Trim(),
            CreadoPor = string.IsNullOrWhiteSpace(op.CreadoPor) ? actor : op.CreadoPor.Trim(),
            CreadoEnUtc = op.Id == 0 ? DateTime.Parse(nowUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : op.CreadoEnUtc
        };
    }

    public async Task UpdateOperacionEstadoAsync(long id, EstadoOperacion nuevoEstado, string motivo, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE operaciones_comerciales
            SET estado = @e,
                observaciones = CASE WHEN @m <> '' THEN TRIM(observaciones || ' | [' || @u || '] ' || @m) ELSE observaciones END
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@e", (int)nuevoEstado);
        cmd.Parameters.AddWithValue("@m", motivo.Trim());
        cmd.Parameters.AddWithValue("@u", actor);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, "OPERACION_ESTADO", "ERP_OPERACIONES",
            id.ToString(CultureInfo.InvariantCulture), $"Operación #{id} -> {nuevoEstado.ToDisplayName()}. Motivo: {motivo}", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static OperacionComercial MapOperacionRow(SqliteDataReader rd) => new()
    {
        Id = rd.GetInt64(0),
        Codigo = rd.GetString(1),
        TipoOperacion = (TipoOperacionComercial)rd.GetInt32(2),
        Fecha = DateOnly.ParseExact(rd.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        RutCliente = rd.GetString(4),
        NombreCliente = rd.GetString(5),
        RutProveedor = rd.GetString(6),
        NombreProveedor = rd.GetString(7),
        CodProducto = rd.GetString(8),
        NombreProducto = rd.GetString(9),
        CantidadEsperadaKg = rd.GetInt64(10),
        CantidadRealConfirmadaKg = rd.GetInt64(11),
        UnidadMedida = rd.GetString(12),
        Patente = rd.GetString(13),
        DocumentoAsociado = rd.GetString(14),
        Estado = (EstadoOperacion)rd.GetInt32(15),
        Observaciones = rd.GetString(16),
        CreadoPor = rd.GetString(17),
        CreadoEnUtc = DateTime.Parse(rd.GetString(18), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };

    // =========================================================================
    // PESAJES INDUSTRIALES (1 Y 2 MEDICIONES + HISTORIAL AUDITADO)
    // =========================================================================

    public async Task<Pesaje?> GetPesajeByIdAsync(long id, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, folio, planta, tipo_pesaje, patente, acoplado, peso_bruto, peso_tara, peso_neto,
                   cod_producto, nom_producto, rut_cliente, nom_cliente, origen, destino, guia_despacho,
                   chofer, transportista, fecha_ingreso, hora_ingreso, fecha_salida, hora_salida,
                   observacion, grupo_material, es_traslado, ciudad_cliente,
                   folio_codigo, unidad_medida, bascula, operador, estado_pesaje, tipo_pesaje_config_id,
                   operacion_id, vehiculo_id, primera_medicion_kg, primera_medicion_utc,
                   segunda_medicion_kg, segunda_medicion_utc, peso_esperado_kg,
                   ajuste_manual_autorizado, motivo_ajuste_anulacion, modificado_por, modificado_en_utc
            FROM pesajes
            WHERE id = @id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@id", id);

        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false) ? MapPesajeRow(rd) : null;
    }

    public async Task<long> GetNextFolioAsync(Planta planta, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(folio), 0) + 1 FROM pesajes WHERE planta = @pl";
        cmd.Parameters.AddWithValue("@pl", (int)planta);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    public async Task<Pesaje> SavePesajeIndustrialAsync(
        Pesaje pesaje,
        string actor,
        string accionAuditoria,
        string? motivoCambio = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        Pesaje? previous = null;
        if (pesaje.Id > 0)
        {
            await using SqliteCommand selPrev = conn.CreateCommand();
            selPrev.Transaction = tx;
            selPrev.CommandText = """
                SELECT id, folio, planta, tipo_pesaje, patente, acoplado, peso_bruto, peso_tara, peso_neto,
                       cod_producto, nom_producto, rut_cliente, nom_cliente, origen, destino, guia_despacho,
                       chofer, transportista, fecha_ingreso, hora_ingreso, fecha_salida, hora_salida,
                       observacion, grupo_material, es_traslado, ciudad_cliente,
                       folio_codigo, unidad_medida, bascula, operador, estado_pesaje, tipo_pesaje_config_id,
                       operacion_id, vehiculo_id, primera_medicion_kg, primera_medicion_utc,
                       segunda_medicion_kg, segunda_medicion_utc, peso_esperado_kg,
                       ajuste_manual_autorizado, motivo_ajuste_anulacion, modificado_por, modificado_en_utc
                FROM pesajes
                WHERE id = @id
                LIMIT 1
                """;
            selPrev.Parameters.AddWithValue("@id", pesaje.Id);
            await using SqliteDataReader rd = await selPrev.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                previous = MapPesajeRow(rd);
            }
        }

        long folio = pesaje.Folio;
        if (folio <= 0)
        {
            await using SqliteCommand nxt = conn.CreateCommand();
            nxt.Transaction = tx;
            nxt.CommandText = "SELECT COALESCE(MAX(folio), 0) + 1 FROM pesajes WHERE planta = @pl";
            nxt.Parameters.AddWithValue("@pl", (int)pesaje.Planta);
            folio = Convert.ToInt64(await nxt.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        string folioCodigo = string.IsNullOrWhiteSpace(pesaje.FolioCodigo)
            ? folio.ToString(CultureInfo.InvariantCulture)
            : pesaje.FolioCodigo.Trim();
        string nowUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        string unidad = string.IsNullOrWhiteSpace(pesaje.UnidadMedida) ? "KG" : pesaje.UnidadMedida.Trim().ToUpperInvariant();
        string operador = string.IsNullOrWhiteSpace(pesaje.Operador) ? actor : pesaje.Operador.Trim();

        long id = pesaje.Id;
        if (id == 0)
        {
            await using SqliteCommand ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO pesajes(
                    folio, planta, tipo_pesaje, patente, acoplado, peso_bruto, peso_tara, peso_neto,
                    cod_producto, nom_producto, rut_cliente, nom_cliente, origen, destino, guia_despacho,
                    chofer, transportista, fecha_ingreso, hora_ingreso, fecha_salida, hora_salida,
                    observacion, grupo_material, es_traslado, ciudad_cliente,
                    folio_codigo, unidad_medida, bascula, operador, estado_pesaje, tipo_pesaje_config_id,
                    operacion_id, vehiculo_id, primera_medicion_kg, primera_medicion_utc,
                    segunda_medicion_kg, segunda_medicion_utc, peso_esperado_kg,
                    ajuste_manual_autorizado, motivo_ajuste_anulacion, modificado_por, modificado_en_utc)
                VALUES(
                    @folio, @pl, @tp, @pat, @aco, @br, @ta, @ne,
                    @cp, @np, @rc, @nc, @or, @de, @gu,
                    @ch, @tr, @fi, @hi, @fs, @hs,
                    @ob, @gm, @et, @ci,
                    @fc, @um, @bas, @op, @est, @tpc,
                    @opid, @vid, @m1k, @m1t,
                    @m2k, @m2t, @pesp,
                    @aju, @mot, @mpor, @mfts);
                SELECT last_insert_rowid();
                """;
            BindIndustrialPesajeParameters(ins, pesaje, folio, folioCodigo, unidad, operador, actor, nowUtc);
            id = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
        else
        {
            await using SqliteCommand upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE pesajes
                SET folio = @folio, planta = @pl, tipo_pesaje = @tp, patente = @pat, acoplado = @aco,
                    peso_bruto = @br, peso_tara = @ta, peso_neto = @ne,
                    cod_producto = @cp, nom_producto = @np, rut_cliente = @rc, nom_cliente = @nc,
                    origen = @or, destino = @de, guia_despacho = @gu, chofer = @ch, transportista = @tr,
                    fecha_ingreso = @fi, hora_ingreso = @hi, fecha_salida = @fs, hora_salida = @hs,
                    observacion = @ob, grupo_material = @gm, es_traslado = @et, ciudad_cliente = @ci,
                    folio_codigo = @fc, unidad_medida = @um, bascula = @bas, operador = @op,
                    estado_pesaje = @est, tipo_pesaje_config_id = @tpc, operacion_id = @opid, vehiculo_id = @vid,
                    primera_medicion_kg = @m1k, primera_medicion_utc = @m1t,
                    segunda_medicion_kg = @m2k, segunda_medicion_utc = @m2t,
                    peso_esperado_kg = @pesp, ajuste_manual_autorizado = @aju,
                    motivo_ajuste_anulacion = @mot, modificado_por = @mpor, modificado_en_utc = @mfts
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("@id", id);
            BindIndustrialPesajeParameters(upd, pesaje, folio, folioCodigo, unidad, operador, actor, nowUtc);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Registrar historial inmutable de cambios del pesaje
        await using (SqliteCommand hist = conn.CreateCommand())
        {
            hist.Transaction = tx;
            hist.CommandText = """
                INSERT INTO pesajes_historial(
                    pesaje_id, timestamp_utc, usuario, accion,
                    peso_bruto_anterior, peso_tara_anterior, peso_neto_anterior,
                    peso_bruto_nuevo, peso_tara_nuevo, peso_neto_nuevo,
                    estado_anterior, estado_nuevo, motivo)
                VALUES(@pid, @ts, @u, @acc, @ba, @ta, @na, @bn, @tn, @nn, @ea, @en, @mot)
                """;
            hist.Parameters.AddWithValue("@pid", id);
            hist.Parameters.AddWithValue("@ts", nowUtc);
            hist.Parameters.AddWithValue("@u", actor);
            hist.Parameters.AddWithValue("@acc", accionAuditoria);
            hist.Parameters.AddWithValue("@ba", previous?.PesoBruto ?? 0);
            hist.Parameters.AddWithValue("@ta", previous?.PesoTara ?? 0);
            hist.Parameters.AddWithValue("@na", previous?.PesoNeto ?? 0);
            hist.Parameters.AddWithValue("@bn", pesaje.PesoBruto);
            hist.Parameters.AddWithValue("@tn", pesaje.PesoTara);
            hist.Parameters.AddWithValue("@nn", pesaje.PesoNeto);
            hist.Parameters.AddWithValue("@ea", (int)(previous?.Estado ?? pesaje.Estado));
            hist.Parameters.AddWithValue("@en", (int)pesaje.Estado);
            hist.Parameters.AddWithValue("@mot", string.IsNullOrWhiteSpace(motivoCambio) ? pesaje.MotivoAjusteOAnulacion : motivoCambio.Trim());
            await hist.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Si el pesaje está vinculado a una operación ERP, recalcular cantidad real confirmada
        if (pesaje.OperacionId.HasValue)
        {
            await RefreshOperacionCantidadRealTxAsync(conn, tx, pesaje.OperacionId.Value, ct).ConfigureAwait(false);
        }

        if (previous?.OperacionId is long prevOpId && prevOpId != pesaje.OperacionId)
        {
            await RefreshOperacionCantidadRealTxAsync(conn, tx, prevOpId, ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, accionAuditoria, "PESAJES", folioCodigo,
            $"Pesaje #{id} (Folio {folioCodigo}, Patente {pesaje.Patente}, Neto {pesaje.PesoNeto:N0} {unidad}, Estado={pesaje.EstadoDisplay}). {motivoCambio ?? string.Empty}".Trim(),
            ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new Pesaje
        {
            Id = id,
            Folio = folio,
            FolioCodigo = folioCodigo,
            Planta = pesaje.Planta,
            TipoPesaje = pesaje.TipoPesaje,
            Patente = pesaje.Patente,
            Acoplado = pesaje.Acoplado,
            PesoBruto = pesaje.PesoBruto,
            PesoTara = pesaje.PesoTara,
            PesoNeto = pesaje.PesoNeto,
            UnidadMedida = unidad,
            CodProducto = pesaje.CodProducto,
            NomProducto = pesaje.NomProducto,
            RutCliente = pesaje.RutCliente,
            NomCliente = pesaje.NomCliente,
            Origen = pesaje.Origen,
            Destino = pesaje.Destino,
            GuiaDespacho = pesaje.GuiaDespacho,
            Chofer = pesaje.Chofer,
            Transportista = pesaje.Transportista,
            FechaIngreso = pesaje.FechaIngreso,
            HoraIngreso = pesaje.HoraIngreso,
            FechaSalida = pesaje.FechaSalida,
            HoraSalida = pesaje.HoraSalida,
            Observacion = pesaje.Observacion,
            GrupoMaterial = pesaje.GrupoMaterial,
            EsTraslado = pesaje.EsTraslado,
            CiudadCliente = pesaje.CiudadCliente,
            Bascula = pesaje.Bascula,
            Operador = operador,
            Estado = pesaje.Estado,
            TipoPesajeConfigId = pesaje.TipoPesajeConfigId,
            OperacionId = pesaje.OperacionId,
            VehiculoId = pesaje.VehiculoId,
            PrimeraMedicionKg = pesaje.PrimeraMedicionKg,
            PrimeraMedicionUtc = pesaje.PrimeraMedicionUtc,
            SegundaMedicionKg = pesaje.SegundaMedicionKg,
            SegundaMedicionUtc = pesaje.SegundaMedicionUtc,
            PesoEsperadoKg = pesaje.PesoEsperadoKg,
            AjusteManualAutorizado = pesaje.AjusteManualAutorizado,
            MotivoAjusteOAnulacion = pesaje.MotivoAjusteOAnulacion,
            ModificadoPor = actor,
            ModificadoEnUtc = DateTime.Parse(nowUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    private static void BindIndustrialPesajeParameters(
        SqliteCommand cmd,
        Pesaje p,
        long folio,
        string folioCodigo,
        string unidad,
        string operador,
        string actor,
        string nowUtc)
    {
        cmd.Parameters.AddWithValue("@folio", folio);
        cmd.Parameters.AddWithValue("@pl", (int)p.Planta);
        cmd.Parameters.AddWithValue("@tp", (int)p.TipoPesaje);
        cmd.Parameters.AddWithValue("@pat", p.Patente.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@aco", p.Acoplado.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@br", p.PesoBruto);
        cmd.Parameters.AddWithValue("@ta", p.PesoTara);
        cmd.Parameters.AddWithValue("@ne", p.PesoNeto);
        cmd.Parameters.AddWithValue("@cp", p.CodProducto.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@np", p.NomProducto.Trim());
        cmd.Parameters.AddWithValue("@rc", p.RutCliente.Trim());
        cmd.Parameters.AddWithValue("@nc", p.NomCliente.Trim());
        cmd.Parameters.AddWithValue("@or", p.Origen.Trim());
        cmd.Parameters.AddWithValue("@de", p.Destino.Trim());
        cmd.Parameters.AddWithValue("@gu", p.GuiaDespacho.Trim());
        cmd.Parameters.AddWithValue("@ch", p.Chofer.Trim());
        cmd.Parameters.AddWithValue("@tr", p.Transportista.Trim());
        cmd.Parameters.AddWithValue("@fi", p.FechaIngreso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@hi", p.HoraIngreso.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@fs", p.FechaSalida.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@hs", p.HoraSalida.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@ob", p.Observacion.Trim());
        cmd.Parameters.AddWithValue("@gm", string.IsNullOrWhiteSpace(p.GrupoMaterial) ? "GENERAL" : p.GrupoMaterial.Trim());
        cmd.Parameters.AddWithValue("@et", p.EsTraslado ? 1 : 0);
        cmd.Parameters.AddWithValue("@ci", (object?)p.CiudadCliente ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fc", folioCodigo);
        cmd.Parameters.AddWithValue("@um", unidad);
        cmd.Parameters.AddWithValue("@bas", p.Bascula.Trim());
        cmd.Parameters.AddWithValue("@op", operador);
        cmd.Parameters.AddWithValue("@est", (int)p.Estado);
        cmd.Parameters.AddWithValue("@tpc", (object?)p.TipoPesajeConfigId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@opid", (object?)p.OperacionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@vid", (object?)p.VehiculoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@m1k", (object?)p.PrimeraMedicionKg ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@m1t", p.PrimeraMedicionUtc?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@m2k", (object?)p.SegundaMedicionKg ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@m2t", p.SegundaMedicionUtc?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pesp", (object?)p.PesoEsperadoKg ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aju", p.AjusteManualAutorizado ? 1 : 0);
        cmd.Parameters.AddWithValue("@mot", p.MotivoAjusteOAnulacion.Trim());
        cmd.Parameters.AddWithValue("@mpor", actor);
        cmd.Parameters.AddWithValue("@mfts", nowUtc);
    }

    public async Task<IReadOnlyList<PesajeHistorialCambio>> GetPesajeHistorialAsync(long pesajeId, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, pesaje_id, timestamp_utc, usuario, accion,
                   peso_bruto_anterior, peso_tara_anterior, peso_neto_anterior,
                   peso_bruto_nuevo, peso_tara_nuevo, peso_neto_nuevo,
                   estado_anterior, estado_nuevo, motivo
            FROM pesajes_historial
            WHERE pesaje_id = @pid
            ORDER BY id DESC
            """;
        cmd.Parameters.AddWithValue("@pid", pesajeId);

        List<PesajeHistorialCambio> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new PesajeHistorialCambio
            {
                Id = rd.GetInt64(0),
                PesajeId = rd.GetInt64(1),
                TimestampUtc = DateTime.Parse(rd.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Usuario = rd.GetString(3),
                Accion = rd.GetString(4),
                PesoBrutoAnterior = rd.GetInt32(5),
                PesoTaraAnterior = rd.GetInt32(6),
                PesoNetoAnterior = rd.GetInt32(7),
                PesoBrutoNuevo = rd.GetInt32(8),
                PesoTaraNuevo = rd.GetInt32(9),
                PesoNetoNuevo = rd.GetInt32(10),
                EstadoAnterior = (EstadoPesaje)rd.GetInt32(11),
                EstadoNuevo = (EstadoPesaje)rd.GetInt32(12),
                Motivo = rd.GetString(13)
            });
        }

        return list;
    }

    // =========================================================================
    // INVENTARIO Y MOVIMIENTOS (LIBRO MAYOR INMUTABLE + IDEMPOTENCIA POR PESAJE)
    // =========================================================================

    public async Task<IReadOnlyList<InventarioExistencia>> GetExistenciasInventarioAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                m.cod_producto,
                COALESCE(NULLIF(p.nombre, ''), NULLIF(p.descripcion, ''), MAX(m.nombre_producto), m.cod_producto) AS nom_prod,
                COALESCE(NULLIF(p.grupo, ''), 'GENERAL') AS grupo_mat,
                COALESCE(NULLIF(p.unidad_medida, ''), MAX(m.unidad_medida), 'KG') AS unidad,
                SUM(CASE WHEN m.cantidad_kg > 0 AND m.tipo_movimiento IN (1, 3) AND m.anulado = 0 THEN m.cantidad_kg ELSE 0 END) AS entradas_kg,
                SUM(CASE WHEN m.cantidad_kg < 0 AND m.tipo_movimiento IN (2, 4) AND m.anulado = 0 THEN ABS(m.cantidad_kg) ELSE 0 END) AS salidas_kg,
                SUM(CASE WHEN m.tipo_movimiento IN (5, 6) AND m.anulado = 0 THEN m.cantidad_kg ELSE 0 END) AS ajustes_kg,
                SUM(m.cantidad_kg) AS stock_actual_kg,
                COUNT(*) AS cant_movs,
                MAX(m.timestamp_utc) AS ultimo_ts
            FROM inventario_movimientos m
            LEFT JOIN productos p ON UPPER(p.codigo) = UPPER(m.cod_producto)
            GROUP BY UPPER(m.cod_producto)
            ORDER BY stock_actual_kg DESC, m.cod_producto ASC
            """;

        List<InventarioExistencia> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            DateTime? ult = rd.IsDBNull(9)
                ? null
                : DateTime.Parse(rd.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            list.Add(new InventarioExistencia(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetString(2),
                rd.GetString(3),
                rd.GetInt64(4),
                rd.GetInt64(5),
                rd.GetInt64(6),
                rd.GetInt64(7),
                rd.GetInt32(8),
                ult));
        }

        return list;
    }

    public async Task<IReadOnlyList<InventarioMovimiento>> GetMovimientosInventarioAsync(
        string? codProducto = null,
        long? operacionId = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();

        List<string> where = [];
        if (!string.IsNullOrWhiteSpace(codProducto))
        {
            where.Add("UPPER(cod_producto) = UPPER(@cp)");
            cmd.Parameters.AddWithValue("@cp", codProducto.Trim());
        }

        if (operacionId.HasValue)
        {
            where.Add("operacion_id = @op");
            cmd.Parameters.AddWithValue("@op", operacionId.Value);
        }

        string clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = $"""
            SELECT id, timestamp_utc, tipo_movimiento, cod_producto, nombre_producto, cantidad_kg, unidad_medida,
                   pesaje_id, folio_pesaje, operacion_id, codigo_operacion, documento_referencia, usuario, observaciones, anulado
            FROM inventario_movimientos
            {clause}
            ORDER BY id DESC
            LIMIT @lim
            """;
        cmd.Parameters.AddWithValue("@lim", Math.Clamp(limit, 1, 5000));

        List<InventarioMovimiento> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapInventarioMovimientoRow(rd));
        }

        return list;
    }

    public async Task<InventarioMovimiento?> PostInventarioParaPesajeConfirmadoAsync(
        Pesaje pesaje,
        TipoMovimientoInventario tipo,
        long cantidadFirmadaKg,
        string actor,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        if (pesaje.Estado != EstadoPesaje.Confirmado || pesaje.Id <= 0 || cantidadFirmadaKg == 0)
        {
            return null;
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        // Verificar si ya existe un movimiento activo para este pesaje (garantía de idempotencia)
        InventarioMovimiento? activeExisting = null;
        await using (SqliteCommand chk = conn.CreateCommand())
        {
            chk.Transaction = tx;
            chk.CommandText = """
                SELECT id, timestamp_utc, tipo_movimiento, cod_producto, nombre_producto, cantidad_kg, unidad_medida,
                       pesaje_id, folio_pesaje, operacion_id, codigo_operacion, documento_referencia, usuario, observaciones, anulado
                FROM inventario_movimientos
                WHERE pesaje_id = @pid AND tipo_movimiento <> 7 AND anulado = 0
                LIMIT 1
                """;
            chk.Parameters.AddWithValue("@pid", pesaje.Id);
            await using SqliteDataReader rd = await chk.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                activeExisting = MapInventarioMovimientoRow(rd);
            }
        }

        if (activeExisting is not null)
        {
            bool samePosting = string.Equals(activeExisting.CodProducto, pesaje.CodProducto, StringComparison.OrdinalIgnoreCase) &&
                               activeExisting.CantidadKg == cantidadFirmadaKg &&
                               activeExisting.TipoMovimiento == tipo;
            if (samePosting)
            {
                // Ya contabilizado con exactamente el mismo valor: idempotencia total (0 duplicados)
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return activeExisting;
            }

            // El pesaje fue modificado con autorización: revertir asiento anterior y emitir nuevo asiento trazable
            await MarkAndReverseMovementTxAsync(conn, tx, activeExisting, $"Reverso por modificación autorizada del pesaje Folio {pesaje.FolioDisplay}.", actor, ct).ConfigureAwait(false);
        }

        string codigoOp = string.Empty;
        if (pesaje.OperacionId.HasValue)
        {
            await using SqliteCommand opCmd = conn.CreateCommand();
            opCmd.Transaction = tx;
            opCmd.CommandText = "SELECT codigo FROM operaciones_comerciales WHERE id = @id LIMIT 1";
            opCmd.Parameters.AddWithValue("@id", pesaje.OperacionId.Value);
            codigoOp = Convert.ToString(await opCmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
        }

        string ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        long newId;
        await using (SqliteCommand ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO inventario_movimientos(
                    timestamp_utc, tipo_movimiento, cod_producto, nombre_producto, cantidad_kg, unidad_medida,
                    pesaje_id, folio_pesaje, operacion_id, codigo_operacion, documento_referencia, usuario, observaciones, anulado)
                VALUES(@ts, @tm, @cp, @np, @cant, @um, @pid, @fol, @opid, @cop, @doc, @usr, @obs, 0);
                SELECT last_insert_rowid();
                """;
            ins.Parameters.AddWithValue("@ts", ts);
            ins.Parameters.AddWithValue("@tm", (int)tipo);
            ins.Parameters.AddWithValue("@cp", pesaje.CodProducto.Trim().ToUpperInvariant());
            ins.Parameters.AddWithValue("@np", pesaje.NomProducto.Trim());
            ins.Parameters.AddWithValue("@cant", cantidadFirmadaKg);
            ins.Parameters.AddWithValue("@um", pesaje.UnidadMedida.Trim().ToUpperInvariant());
            ins.Parameters.AddWithValue("@pid", pesaje.Id);
            ins.Parameters.AddWithValue("@fol", pesaje.FolioDisplay);
            ins.Parameters.AddWithValue("@opid", (object?)pesaje.OperacionId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@cop", codigoOp);
            ins.Parameters.AddWithValue("@doc", pesaje.GuiaDespacho.Trim());
            ins.Parameters.AddWithValue("@usr", actor);
            ins.Parameters.AddWithValue("@obs", $"Pesaje confirmado Folio {pesaje.FolioDisplay} ({pesaje.Patente}).");
            newId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        await AddAuditTxAsync(conn, tx, actor, "INVENTARIO_MOVIMIENTO_PESAJE", "INVENTARIO",
            newId.ToString(CultureInfo.InvariantCulture),
            $"Asiento {tipo.ToDisplayName()} {cantidadFirmadaKg:+#,##0;-#,##0;0} {pesaje.UnidadMedida} de {pesaje.CodProducto} (Pesaje Folio {pesaje.FolioDisplay}).",
            ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new InventarioMovimiento
        {
            Id = newId,
            TimestampUtc = DateTime.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TipoMovimiento = tipo,
            CodProducto = pesaje.CodProducto.Trim().ToUpperInvariant(),
            NombreProducto = pesaje.NomProducto.Trim(),
            CantidadKg = cantidadFirmadaKg,
            UnidadMedida = pesaje.UnidadMedida.Trim().ToUpperInvariant(),
            PesajeId = pesaje.Id,
            FolioPesaje = pesaje.FolioDisplay,
            OperacionId = pesaje.OperacionId,
            CodigoOperacion = codigoOp,
            DocumentoReferencia = pesaje.GuiaDespacho.Trim(),
            Usuario = actor,
            Observaciones = $"Pesaje confirmado Folio {pesaje.FolioDisplay} ({pesaje.Patente}).",
            Anulado = false
        };
    }

    public async Task<InventarioMovimiento?> RevertirInventarioParaPesajeAnuladoAsync(
        Pesaje pesaje,
        string motivo,
        string actor,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        if (pesaje.Id <= 0)
        {
            return null;
        }

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        InventarioMovimiento? activeExisting = null;
        await using (SqliteCommand chk = conn.CreateCommand())
        {
            chk.Transaction = tx;
            chk.CommandText = """
                SELECT id, timestamp_utc, tipo_movimiento, cod_producto, nombre_producto, cantidad_kg, unidad_medida,
                       pesaje_id, folio_pesaje, operacion_id, codigo_operacion, documento_referencia, usuario, observaciones, anulado
                FROM inventario_movimientos
                WHERE pesaje_id = @pid AND tipo_movimiento <> 7 AND anulado = 0
                LIMIT 1
                """;
            chk.Parameters.AddWithValue("@pid", pesaje.Id);
            await using SqliteDataReader rd = await chk.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                activeExisting = MapInventarioMovimientoRow(rd);
            }
        }

        if (activeExisting is null)
        {
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return null;
        }

        InventarioMovimiento reversal = await MarkAndReverseMovementTxAsync(
            conn,
            tx,
            activeExisting,
            $"Reverso por anulación de pesaje Folio {pesaje.FolioDisplay}: {motivo}",
            actor,
            ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, "INVENTARIO_REVERSO_ANULACION", "INVENTARIO",
            reversal.Id.ToString(CultureInfo.InvariantCulture),
            $"Reverso de {reversal.CantidadKg:+#,##0;-#,##0;0} {reversal.UnidadMedida} para {reversal.CodProducto} por anulación Folio {pesaje.FolioDisplay}.",
            ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return reversal;
    }

    public async Task<InventarioMovimiento> RegistrarAjusteInventarioAsync(
        string codProducto,
        long cantidadFirmadaKg,
        string documentoRef,
        string motivo,
        string actor,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(codProducto))
        {
            throw new ArgumentException("Debe seleccionar un material para el ajuste de inventario.");
        }

        if (cantidadFirmadaKg == 0)
        {
            throw new ArgumentException("La cantidad del ajuste debe ser distinta de cero.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("Debe indicar un motivo auditable para el ajuste de inventario.");
        }

        Producto? prod = await GetMaterialByCodigoAsync(codProducto, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"El material '{codProducto}' no existe en el catálogo maestro.");

        TipoMovimientoInventario tipo = cantidadFirmadaKg > 0
            ? TipoMovimientoInventario.AjustePositivo
            : TipoMovimientoInventario.AjusteNegativo;

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        string ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await using SqliteCommand ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO inventario_movimientos(
                timestamp_utc, tipo_movimiento, cod_producto, nombre_producto, cantidad_kg, unidad_medida,
                pesaje_id, folio_pesaje, operacion_id, codigo_operacion, documento_referencia, usuario, observaciones, anulado)
            VALUES(@ts, @tm, @cp, @np, @cant, @um, NULL, '', NULL, '', @doc, @usr, @obs, 0);
            SELECT last_insert_rowid();
            """;
        ins.Parameters.AddWithValue("@ts", ts);
        ins.Parameters.AddWithValue("@tm", (int)tipo);
        ins.Parameters.AddWithValue("@cp", prod.Codigo);
        ins.Parameters.AddWithValue("@np", prod.NombreVisible);
        ins.Parameters.AddWithValue("@cant", cantidadFirmadaKg);
        ins.Parameters.AddWithValue("@um", prod.UnidadMedida);
        ins.Parameters.AddWithValue("@doc", documentoRef.Trim());
        ins.Parameters.AddWithValue("@usr", actor);
        ins.Parameters.AddWithValue("@obs", motivo.Trim());
        long id = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

        await AddAuditTxAsync(conn, tx, actor, "INVENTARIO_AJUSTE_AUTORIZADO", "INVENTARIO",
            id.ToString(CultureInfo.InvariantCulture),
            $"{tipo.ToDisplayName()} {cantidadFirmadaKg:+#,##0;-#,##0;0} {prod.UnidadMedida} de {prod.Codigo}. Doc='{documentoRef}'. Motivo: {motivo}",
            ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new InventarioMovimiento
        {
            Id = id,
            TimestampUtc = DateTime.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TipoMovimiento = tipo,
            CodProducto = prod.Codigo,
            NombreProducto = prod.NombreVisible,
            CantidadKg = cantidadFirmadaKg,
            UnidadMedida = prod.UnidadMedida,
            DocumentoReferencia = documentoRef.Trim(),
            Usuario = actor,
            Observaciones = motivo.Trim(),
            Anulado = false
        };
    }

    private static async Task<InventarioMovimiento> MarkAndReverseMovementTxAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        InventarioMovimiento original,
        string motivo,
        string actor,
        CancellationToken ct)
    {
        await using (SqliteCommand mark = conn.CreateCommand())
        {
            mark.Transaction = tx;
            mark.CommandText = "UPDATE inventario_movimientos SET anulado = 1 WHERE id = @id";
            mark.Parameters.AddWithValue("@id", original.Id);
            await mark.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        string ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        long reverseQty = -original.CantidadKg;

        await using SqliteCommand ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO inventario_movimientos(
                timestamp_utc, tipo_movimiento, cod_producto, nombre_producto, cantidad_kg, unidad_medida,
                pesaje_id, folio_pesaje, operacion_id, codigo_operacion, documento_referencia, usuario, observaciones, anulado)
            VALUES(@ts, @tm, @cp, @np, @cant, @um, @pid, @fol, @opid, @cop, @doc, @usr, @obs, 0);
            SELECT last_insert_rowid();
            """;
        ins.Parameters.AddWithValue("@ts", ts);
        ins.Parameters.AddWithValue("@tm", (int)TipoMovimientoInventario.ReversoAnulacion);
        ins.Parameters.AddWithValue("@cp", original.CodProducto);
        ins.Parameters.AddWithValue("@np", original.NombreProducto);
        ins.Parameters.AddWithValue("@cant", reverseQty);
        ins.Parameters.AddWithValue("@um", original.UnidadMedida);
        ins.Parameters.AddWithValue("@pid", (object?)original.PesajeId ?? DBNull.Value);
        ins.Parameters.AddWithValue("@fol", original.FolioPesaje);
        ins.Parameters.AddWithValue("@opid", (object?)original.OperacionId ?? DBNull.Value);
        ins.Parameters.AddWithValue("@cop", original.CodigoOperacion);
        ins.Parameters.AddWithValue("@doc", original.DocumentoReferencia);
        ins.Parameters.AddWithValue("@usr", actor);
        ins.Parameters.AddWithValue("@obs", motivo.Trim());
        long revId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

        return new InventarioMovimiento
        {
            Id = revId,
            TimestampUtc = DateTime.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TipoMovimiento = TipoMovimientoInventario.ReversoAnulacion,
            CodProducto = original.CodProducto,
            NombreProducto = original.NombreProducto,
            CantidadKg = reverseQty,
            UnidadMedida = original.UnidadMedida,
            PesajeId = original.PesajeId,
            FolioPesaje = original.FolioPesaje,
            OperacionId = original.OperacionId,
            CodigoOperacion = original.CodigoOperacion,
            DocumentoReferencia = original.DocumentoReferencia,
            Usuario = actor,
            Observaciones = motivo.Trim(),
            Anulado = false
        };
    }

    private static async Task RefreshOperacionCantidadRealTxAsync(SqliteConnection conn, SqliteTransaction tx, long operacionId, CancellationToken ct)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE operaciones_comerciales
            SET cantidad_real_kg = COALESCE(
                (SELECT SUM(peso_neto) FROM pesajes WHERE operacion_id = @opid AND estado_pesaje = 2), 0),
                estado = CASE
                    WHEN estado = 4 THEN 4
                    WHEN COALESCE((SELECT SUM(peso_neto) FROM pesajes WHERE operacion_id = @opid AND estado_pesaje = 2), 0) >= cantidad_esperada_kg AND cantidad_esperada_kg > 0 THEN 3
                    WHEN COALESCE((SELECT COUNT(*) FROM pesajes WHERE operacion_id = @opid AND estado_pesaje IN (1, 2, 4)), 0) > 0 THEN 2
                    ELSE estado
                END
            WHERE id = @opid
            """;
        cmd.Parameters.AddWithValue("@opid", operacionId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static InventarioMovimiento MapInventarioMovimientoRow(SqliteDataReader rd) => new()
    {
        Id = rd.GetInt64(0),
        TimestampUtc = DateTime.Parse(rd.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        TipoMovimiento = (TipoMovimientoInventario)rd.GetInt32(2),
        CodProducto = rd.GetString(3),
        NombreProducto = rd.GetString(4),
        CantidadKg = rd.GetInt64(5),
        UnidadMedida = rd.GetString(6),
        PesajeId = rd.IsDBNull(7) ? null : rd.GetInt64(7),
        FolioPesaje = rd.GetString(8),
        OperacionId = rd.IsDBNull(9) ? null : rd.GetInt64(9),
        CodigoOperacion = rd.GetString(10),
        DocumentoReferencia = rd.GetString(11),
        Usuario = rd.GetString(12),
        Observaciones = rd.GetString(13),
        Anulado = rd.GetInt32(14) == 1
    };

    private static async Task<long> CountScalarTxAsync(SqliteConnection conn, SqliteTransaction tx, string sql, string paramName, object value, CancellationToken ct)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue(paramName, value);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }
}
