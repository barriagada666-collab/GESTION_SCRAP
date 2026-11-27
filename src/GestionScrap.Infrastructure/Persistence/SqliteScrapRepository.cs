using System.Globalization;
using System.Text.Json;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using Microsoft.Data.Sqlite;

namespace GestionScrap.Infrastructure.Persistence;

/// <summary>
/// Repositorio SQLite (WAL) con soporte completo para árbol CRUD de Grupos de Material (Nodos/Hijos),
/// perfiles configurables de usuario, cuenta administrativa barriagada y almacenamiento limpio de pesajes.
/// </summary>
public sealed class SqliteScrapRepository : IScrapRepository, IDisposable
{
    private readonly string _connectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly (string Codigo, string Descripcion)[] KnownWmdbspcProducts =
    [
        ("0000000005", "VENTA DE FIERROS"),
        ("0000000006", "CARTON"),
        ("0000000007", "MANGA DE RIEGO"),
        ("0000000008", "MIX SUPERMERCADO MAQUILADO"),
        ("0000000010", "BASURA (DESECHO)"),
        ("0000000013", "DESECHO MIX SUPERMECADO MAQ."),
        ("0000000014", "DESECHO AGRICOLA MAQUILADO"),
        ("5300000004", "SCRAP HDPE COMPRADO"),
        ("5300000005", "SCRAP HDPE COMPRADO AGROSUPER"),
        ("5300000006", "SCRAP HDPE COMPRADO P/LAVADO"),
        ("5300000007", "SCRAP HDPE TRIPLE LAVADO"),
        ("5300000008", "SCRAP HDPE COMPRADO RIGIDO"),
        ("5300000009", "SCRAP MIX SUPERMERCADO"),
        ("5300000010", "SCRAP MIX POST INDUSTRIA"),
        ("5301000001", "SCRAP AGRICOLA CINTA RIEGO"),
        ("5301000002", "SCRAP MIX POST. IND. VIRUTA"),
        ("5301000003", "SCRAP COLOR PICADO PREMIUM"),
        ("5301000005", "SCRAP PESQUERA BLANCO"),
        ("5301000006", "SCRAP AGRICOLA PRE LAVADO >80 MIC."),
        ("5301000007", "SCRAP PESQUERA TRANSPARENTE"),
        ("5301001000", "SCRAP PESQUERA COLOR PRE LAVADO"),
        ("5301001001", "SCRAP MIX POST. IND. PASAS"),
        ("5301001002", "SCRAP AGRICOLA LAVADO PRE LAVADO"),
        ("5301001003", "SCRAP MAQUILADO"),
        ("5301001004", "SCRAP MAQUILADO VIRUTA (SUP)"),
        ("5301002000", "SCRAP COLOR CONTAMINADO P/L (SUP)"),
        ("5301002001", "SCRAP COLOR SIN CONTAMINACION"),
        ("5301002002", "SCRAP PESQUERA TRANSPARENTE PRE LAVADO"),
        ("5301002003", "SCRAP TRANSPARENTE S/CONTAMINACION"),
        ("5301002004", "SCRAP PESQUERA COLOR"),
        ("5301003000", "SCRAP COLOR LIMPIO"),
        ("5301003001", "SCRAP TRANSPARENTE LIMPIO"),
        ("5301003002", "SCRAP BOBINA BLANCA(RECAM)"),
        ("5301003003", "SCRAP BOBINA TRANSP.(RECAM)"),
        ("5301004000", "SCRAP LD TRANSPARENTE EDELPA (RECAM)"),
        ("5301004001", "SCRAP COMPRADO IMPORTADO"),
        ("5301008008", "PLUMILLA AGLOMERADA"),
        ("5302000000", "SCRAP POLIPROPILENO INTERNO"),
        ("5302000001", "SCRAP PVC INTERNO"),
        ("5302000002", "SCRAP ALUMINIO INTERNO"),
        ("5302000004", "SCRAP STRETCH TRANSPARENTE"),
        ("5302000008", "SCRAP AGRICOLA BAJA DENSIDAD BOLO"),
        ("5310000002", "PELLET HDPE COLOR"),
        ("5311000001", "PELLET LDPE COMPRADO"),
        ("5311000007", "PELLET LD STRETCH FILM"),
        ("5389999999", "MERMAS"),
        ("9999999999", "SACAS PLUMILLAS"),
        ("TIX-14", "COAGULANTE")
    ];

    public string DatabasePath { get; }

    public SqliteScrapRepository(string dbPath)
    {
        DatabasePath = dbPath;
        string? dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 10
        };
        _connectionString = builder.ToString();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        SqliteConnection conn = new(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using SqliteCommand pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return conn;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
            const string ddl = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA foreign_keys=ON;

                CREATE TABLE IF NOT EXISTS pesajes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    folio INTEGER NOT NULL,
                    planta INTEGER NOT NULL,
                    tipo_pesaje INTEGER NOT NULL,
                    patente TEXT NOT NULL,
                    acoplado TEXT NOT NULL DEFAULT '',
                    peso_bruto INTEGER NOT NULL,
                    peso_tara INTEGER NOT NULL,
                    peso_neto INTEGER NOT NULL,
                    cod_producto TEXT NOT NULL,
                    nom_producto TEXT NOT NULL,
                    rut_cliente TEXT NOT NULL,
                    nom_cliente TEXT NOT NULL,
                    origen TEXT NOT NULL DEFAULT '',
                    destino TEXT NOT NULL DEFAULT '',
                    guia_despacho TEXT NOT NULL DEFAULT '',
                    chofer TEXT NOT NULL DEFAULT '',
                    transportista TEXT NOT NULL DEFAULT '',
                    fecha_ingreso TEXT NOT NULL,
                    hora_ingreso TEXT NOT NULL,
                    fecha_salida TEXT NOT NULL,
                    hora_salida TEXT NOT NULL,
                    observacion TEXT NOT NULL DEFAULT '',
                    grupo_material TEXT NOT NULL DEFAULT 'GENERAL',
                    es_traslado INTEGER NOT NULL DEFAULT 0,
                    ciudad_cliente TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_pesajes_planta_fecha ON pesajes(planta, fecha_ingreso);
                CREATE INDEX IF NOT EXISTS idx_pesajes_folio ON pesajes(planta, folio);
                CREATE INDEX IF NOT EXISTS idx_pesajes_rut ON pesajes(rut_cliente);
                CREATE INDEX IF NOT EXISTS idx_pesajes_prod ON pesajes(cod_producto);

                CREATE TABLE IF NOT EXISTS clientes (
                    rut TEXT PRIMARY KEY,
                    digito TEXT NOT NULL DEFAULT '',
                    nombre TEXT NOT NULL,
                    razon_social TEXT NOT NULL,
                    giro TEXT NOT NULL DEFAULT '',
                    comuna TEXT NOT NULL DEFAULT '',
                    ciudad TEXT NOT NULL DEFAULT '',
                    direccion TEXT NOT NULL DEFAULT '',
                    telefono TEXT NOT NULL DEFAULT '',
                    planta_asociada INTEGER
                );

                CREATE TABLE IF NOT EXISTS productos (
                    codigo TEXT PRIMARY KEY,
                    descripcion TEXT NOT NULL,
                    grupo TEXT NOT NULL DEFAULT 'GENERAL',
                    unidad_medida TEXT NOT NULL DEFAULT 'KG'
                );

                CREATE TABLE IF NOT EXISTS material_group_nodes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    parent_id INTEGER NULL REFERENCES material_group_nodes(id) ON DELETE CASCADE,
                    nombre TEXT NOT NULL,
                    tipo_nodo INTEGER NOT NULL,
                    codigo_producto TEXT NOT NULL DEFAULT '',
                    color_hex TEXT NOT NULL DEFAULT '#0891B2',
                    orden INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_mat_nodes_parent ON material_group_nodes(parent_id, orden);

                CREATE TABLE IF NOT EXISTS role_profiles (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    nombre TEXT NOT NULL UNIQUE,
                    descripcion TEXT NOT NULL DEFAULT '',
                    es_sistema INTEGER NOT NULL DEFAULT 0,
                    permisos_json TEXT NOT NULL DEFAULT '[]'
                );

                CREATE TABLE IF NOT EXISTS usuarios (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    nombre_usuario TEXT NOT NULL UNIQUE,
                    nombre_completo TEXT NOT NULL,
                    perfil_id INTEGER NOT NULL REFERENCES role_profiles(id),
                    password_hash TEXT NOT NULL,
                    password_salt TEXT NOT NULL,
                    activo INTEGER NOT NULL DEFAULT 1,
                    intentos_fallidos INTEGER NOT NULL DEFAULT 0,
                    bloqueado_hasta_utc TEXT,
                    permisos_json TEXT NOT NULL DEFAULT '[]',
                    creado_en_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS audit_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp_utc TEXT NOT NULL,
                    usuario TEXT NOT NULL,
                    accion TEXT NOT NULL,
                    entidad TEXT NOT NULL,
                    registro_id TEXT NOT NULL DEFAULT '',
                    detalle TEXT NOT NULL DEFAULT ''
                );

                CREATE INDEX IF NOT EXISTS idx_audit_ts ON audit_log(timestamp_utc DESC);

                CREATE TABLE IF NOT EXISTS import_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp_utc TEXT NOT NULL,
                    usuario TEXT NOT NULL,
                    tipo_origen TEXT NOT NULL,
                    origen_detalle TEXT NOT NULL,
                    planta_destino TEXT NOT NULL,
                    destino_detalle TEXT NOT NULL,
                    rango_filtro TEXT NOT NULL,
                    leidos_origen INTEGER NOT NULL DEFAULT 0,
                    importados_nuevos INTEGER NOT NULL DEFAULT 0,
                    duplicados_omitidos INTEGER NOT NULL DEFAULT 0,
                    clientes_sincronizados INTEGER NOT NULL DEFAULT 0,
                    productos_sincronizados INTEGER NOT NULL DEFAULT 0,
                    kilos_importados INTEGER NOT NULL DEFAULT 0,
                    estado TEXT NOT NULL DEFAULT 'COMPLETADA',
                    observacion TEXT NOT NULL DEFAULT ''
                );

                CREATE INDEX IF NOT EXISTS idx_import_hist_ts ON import_history(timestamp_utc DESC);

                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version INTEGER PRIMARY KEY,
                    description TEXT NOT NULL,
                    applied_at_utc TEXT NOT NULL
                );
                """;

            await using (SqliteCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = ddl;
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await ApplyMigrationsAsync(conn, ct).ConfigureAwait(false);
            await SeedProfilesAndAdminAsync(conn, ct).ConfigureAwait(false);
            await SeedInitialCatalogAndMaterialTreeAsync(conn, ct).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task ApplyMigrationsAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using SqliteTransaction tx = conn.BeginTransaction();

        HashSet<int> applied = [];
        await using (SqliteCommand q = conn.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = "SELECT version FROM schema_migrations";
            await using SqliteDataReader rd = await q.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                applied.Add(rd.GetInt32(0));
            }
        }

        if (!applied.Contains(1))
        {
            await RecordMigrationTxAsync(conn, tx, 1, "v4.0.0 base schema (WAL, pesajes, arbol, perfiles RBAC, import_history)", ct).ConfigureAwait(false);
        }

        if (!applied.Contains(2))
        {
            const string perfIndexesSql = """
                CREATE INDEX IF NOT EXISTS idx_pesajes_planta_fecha_folio ON pesajes(planta, fecha_ingreso DESC, folio DESC);
                CREATE INDEX IF NOT EXISTS idx_pesajes_fecha_folio ON pesajes(fecha_ingreso DESC, folio DESC);
                CREATE INDEX IF NOT EXISTS idx_pesajes_folio_hora ON pesajes(planta, folio, hora_ingreso);
                CREATE INDEX IF NOT EXISTS idx_audit_accion_id ON audit_log(accion, id DESC);
                CREATE INDEX IF NOT EXISTS idx_audit_usuario_id ON audit_log(usuario COLLATE NOCASE, id DESC);
                """;
            await using SqliteCommand idxCmd = conn.CreateCommand();
            idxCmd.Transaction = tx;
            idxCmd.CommandText = perfIndexesSql;
            await idxCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await RecordMigrationTxAsync(conn, tx, 2, "v4.1.0 composite covering indexes for pesajes and audit_log", ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static async Task RecordMigrationTxAsync(SqliteConnection conn, SqliteTransaction tx, int version, string description, CancellationToken ct)
    {
        await using SqliteCommand ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = "INSERT OR IGNORE INTO schema_migrations(version, description, applied_at_utc) VALUES(@v, @d, @ts)";
        ins.Parameters.AddWithValue("@v", version);
        ins.Parameters.AddWithValue("@d", description);
        ins.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task SeedProfilesAndAdminAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using (SqliteCommand chk = conn.CreateCommand())
        {
            chk.Transaction = tx;
            chk.CommandText = "SELECT COUNT(*) FROM role_profiles";
            long count = Convert.ToInt64(await chk.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (count == 0)
            {
                foreach (PerfilUsuario profile in DefaultRoleProfiles.GetSeededProfiles())
                {
                    await using SqliteCommand ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = """
                        INSERT INTO role_profiles(id, nombre, descripcion, es_sistema, permisos_json)
                        VALUES(@id, @nom, @desc, @sys, @perm)
                        """;
                    ins.Parameters.AddWithValue("@id", profile.Id);
                    ins.Parameters.AddWithValue("@nom", profile.Nombre);
                    ins.Parameters.AddWithValue("@desc", profile.Descripcion);
                    ins.Parameters.AddWithValue("@sys", profile.EsSistema ? 1 : 0);
                    ins.Parameters.AddWithValue("@perm", JsonSerializer.Serialize(profile.Permisos));
                    await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
        }

        await using (SqliteCommand chkUsr = conn.CreateCommand())
        {
            chkUsr.Transaction = tx;
            chkUsr.CommandText = "SELECT COUNT(*) FROM usuarios WHERE LOWER(nombre_usuario) = 'barriagada'";
            long hasAdmin = Convert.ToInt64(await chkUsr.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (hasAdmin == 0)
            {
                (string hash, string salt) = PasswordSecurity.HashPassword("Cambias0");
                await using SqliteCommand insUser = conn.CreateCommand();
                insUser.Transaction = tx;
                insUser.CommandText = """
                    INSERT INTO usuarios(nombre_usuario, nombre_completo, perfil_id, password_hash, password_salt, activo, intentos_fallidos, permisos_json, creado_en_utc)
                    VALUES('barriagada', 'Boris Arriagada (Administrador)', 1, @h, @s, 1, 0, '[]', @ts)
                    """;
                insUser.Parameters.AddWithValue("@h", hash);
                insUser.Parameters.AddWithValue("@s", salt);
                insUser.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await insUser.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static async Task SeedInitialCatalogAndMaterialTreeAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using (SqliteCommand chkProd = conn.CreateCommand())
        {
            chkProd.Transaction = tx;
            chkProd.CommandText = "SELECT COUNT(*) FROM productos";
            long pCount = Convert.ToInt64(await chkProd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (pCount == 0)
            {
                foreach ((string codigo, string desc) in KnownWmdbspcProducts)
                {
                    await using SqliteCommand insP = conn.CreateCommand();
                    insP.Transaction = tx;
                    insP.CommandText = """
                        INSERT OR IGNORE INTO productos(codigo, descripcion, grupo, unidad_medida)
                        VALUES(@c, @d, @g, 'KG')
                        """;
                    insP.Parameters.AddWithValue("@c", codigo);
                    insP.Parameters.AddWithValue("@d", desc);
                    insP.Parameters.AddWithValue("@g", BusinessRules.ClassifyProductoDefaultGroup(codigo, desc));
                    await insP.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
        }

        await using (SqliteCommand chkTree = conn.CreateCommand())
        {
            chkTree.Transaction = tx;
            chkTree.CommandText = "SELECT COUNT(*) FROM material_group_nodes";
            long nCount = Convert.ToInt64(await chkTree.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (nCount == 0)
            {
                Dictionary<string, long> parentIds = new(StringComparer.OrdinalIgnoreCase);
                int rootOrd = 1;
                foreach ((string nombre, string color) in BusinessRules.DefaultParentGroups)
                {
                    await using SqliteCommand insParent = conn.CreateCommand();
                    insParent.Transaction = tx;
                    insParent.CommandText = """
                        INSERT INTO material_group_nodes(parent_id, nombre, tipo_nodo, codigo_producto, color_hex, orden)
                        VALUES(NULL, @nom, @tipo, '', @col, @ord);
                        SELECT last_insert_rowid();
                        """;
                    insParent.Parameters.AddWithValue("@nom", nombre);
                    insParent.Parameters.AddWithValue("@tipo", (int)NodoGrupoTipo.CarpetaGrupo);
                    insParent.Parameters.AddWithValue("@col", color);
                    insParent.Parameters.AddWithValue("@ord", rootOrd++);
                    long parentId = Convert.ToInt64(await insParent.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
                    parentIds[nombre] = parentId;
                }

                Dictionary<long, int> childOrderCounters = [];
                foreach ((string codigo, string desc) in KnownWmdbspcProducts)
                {
                    string parentName = BusinessRules.ClassifyProductoDefaultGroup(codigo, desc);
                    long pid = parentIds[parentName];
                    int ord = childOrderCounters.TryGetValue(pid, out int cur) ? cur + 1 : 1;
                    childOrderCounters[pid] = ord;

                    await using SqliteCommand insChild = conn.CreateCommand();
                    insChild.Transaction = tx;
                    insChild.CommandText = """
                        INSERT INTO material_group_nodes(parent_id, nombre, tipo_nodo, codigo_producto, color_hex, orden)
                        VALUES(@pid, @nom, @tipo, @cod, '#0E5A7A', @ord)
                        """;
                    insChild.Parameters.AddWithValue("@pid", pid);
                    insChild.Parameters.AddWithValue("@nom", $"{codigo} · {desc}");
                    insChild.Parameters.AddWithValue("@tipo", (int)NodoGrupoTipo.ProductoAsignado);
                    insChild.Parameters.AddWithValue("@cod", codigo);
                    insChild.Parameters.AddWithValue("@ord", ord);
                    await insChild.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Pesaje>> GetPesajesAsync(
        Planta? planta = null,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

        List<string> where = [];
        await using SqliteCommand cmd = conn.CreateCommand();

        if (planta.HasValue)
        {
            where.Add("planta = @planta");
            cmd.Parameters.AddWithValue("@planta", (int)planta.Value);
        }

        if (desde.HasValue)
        {
            where.Add("fecha_ingreso >= @desde");
            cmd.Parameters.AddWithValue("@desde", desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (hasta.HasValue)
        {
            where.Add("fecha_ingreso <= @hasta");
            cmd.Parameters.AddWithValue("@hasta", hasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        string clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = $"""
            SELECT id, folio, planta, tipo_pesaje, patente, acoplado, peso_bruto, peso_tara, peso_neto,
                   cod_producto, nom_producto, rut_cliente, nom_cliente, origen, destino, guia_despacho,
                   chofer, transportista, fecha_ingreso, hora_ingreso, fecha_salida, hora_salida,
                   observacion, grupo_material, es_traslado, ciudad_cliente
            FROM pesajes
            {clause}
            ORDER BY fecha_ingreso DESC, folio DESC
            """;

        List<Pesaje> results = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new Pesaje
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
                CiudadCliente = rd.IsDBNull(25) ? null : rd.GetString(25)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<Cliente>> GetClientesAsync(Planta? planta = null, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = planta.HasValue
            ? "SELECT rut, digito, nombre, razon_social, giro, comuna, ciudad, direccion, telefono, planta_asociada FROM clientes WHERE planta_asociada IS NULL OR planta_asociada = @p ORDER BY razon_social"
            : "SELECT rut, digito, nombre, razon_social, giro, comuna, ciudad, direccion, telefono, planta_asociada FROM clientes ORDER BY razon_social";
        if (planta.HasValue)
        {
            cmd.Parameters.AddWithValue("@p", (int)planta.Value);
        }

        List<Cliente> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new Cliente
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
                PlantaAsociada = rd.IsDBNull(9) ? null : (Planta)rd.GetInt32(9)
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<Producto>> GetProductosAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT codigo, descripcion, grupo, unidad_medida FROM productos ORDER BY codigo";

        List<Producto> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new Producto
            {
                Codigo = rd.GetString(0),
                Descripcion = rd.GetString(1),
                Grupo = rd.GetString(2),
                UnidadMedida = rd.GetString(3)
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<MaterialGroupNode>> GetMaterialNodesAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, parent_id, nombre, tipo_nodo, codigo_producto, color_hex, orden
            FROM material_group_nodes
            ORDER BY COALESCE(parent_id, 0), orden, nombre
            """;

        List<MaterialGroupNode> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new MaterialGroupNode
            {
                Id = rd.GetInt64(0),
                ParentId = rd.IsDBNull(1) ? null : rd.GetInt64(1),
                Nombre = rd.GetString(2),
                TipoNodo = (NodoGrupoTipo)rd.GetInt32(3),
                CodigoProducto = rd.GetString(4),
                ColorHex = rd.GetString(5),
                Orden = rd.GetInt32(6)
            });
        }

        return list;
    }

    public async Task<MaterialGroupNode> SaveMaterialNodeAsync(MaterialGroupNode node, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        long id = node.Id;
        int orden = node.Orden;
        if (id == 0)
        {
            if (orden <= 0)
            {
                await using SqliteCommand maxOrd = conn.CreateCommand();
                maxOrd.Transaction = tx;
                maxOrd.CommandText = "SELECT COALESCE(MAX(orden), 0) + 1 FROM material_group_nodes WHERE COALESCE(parent_id, 0) = @pid";
                maxOrd.Parameters.AddWithValue("@pid", node.ParentId ?? 0L);
                orden = Convert.ToInt32(await maxOrd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            }

            await using SqliteCommand ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO material_group_nodes(parent_id, nombre, tipo_nodo, codigo_producto, color_hex, orden)
                VALUES(@pid, @nom, @tipo, @cod, @col, @ord);
                SELECT last_insert_rowid();
                """;
            ins.Parameters.AddWithValue("@pid", (object?)node.ParentId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@nom", node.Nombre.Trim());
            ins.Parameters.AddWithValue("@tipo", (int)node.TipoNodo);
            ins.Parameters.AddWithValue("@cod", node.CodigoProducto.Trim().ToUpperInvariant());
            ins.Parameters.AddWithValue("@col", string.IsNullOrWhiteSpace(node.ColorHex) ? "#0891B2" : node.ColorHex);
            ins.Parameters.AddWithValue("@ord", orden);
            id = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
        else
        {
            await using SqliteCommand upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE material_group_nodes
                SET parent_id = @pid, nombre = @nom, tipo_nodo = @tipo, codigo_producto = @cod, color_hex = @col, orden = @ord
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("@id", id);
            upd.Parameters.AddWithValue("@pid", (object?)node.ParentId ?? DBNull.Value);
            upd.Parameters.AddWithValue("@nom", node.Nombre.Trim());
            upd.Parameters.AddWithValue("@tipo", (int)node.TipoNodo);
            upd.Parameters.AddWithValue("@cod", node.CodigoProducto.Trim().ToUpperInvariant());
            upd.Parameters.AddWithValue("@col", string.IsNullOrWhiteSpace(node.ColorHex) ? "#0891B2" : node.ColorHex);
            upd.Parameters.AddWithValue("@ord", orden);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, usuario, node.Id == 0 ? "NODO_GRUPO_CREAR" : "NODO_GRUPO_EDITAR", "GRUPO_MATERIAL",
            id.ToString(CultureInfo.InvariantCulture), $"Nodo '{node.Nombre}' ({node.TipoNodo}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new MaterialGroupNode
        {
            Id = id,
            ParentId = node.ParentId,
            Nombre = node.Nombre.Trim(),
            TipoNodo = node.TipoNodo,
            CodigoProducto = node.CodigoProducto.Trim().ToUpperInvariant(),
            ColorHex = string.IsNullOrWhiteSpace(node.ColorHex) ? "#0891B2" : node.ColorHex,
            Orden = orden
        };
    }

    public async Task MoveMaterialNodeAsync(long nodeId, long? newParentId, int newOrden, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        int resolvedOrden = newOrden;
        if (resolvedOrden <= 0 || resolvedOrden >= 999)
        {
            await using SqliteCommand maxOrd = conn.CreateCommand();
            maxOrd.Transaction = tx;
            maxOrd.CommandText = "SELECT COALESCE(MAX(orden), 0) + 1 FROM material_group_nodes WHERE COALESCE(parent_id, 0) = @pid";
            maxOrd.Parameters.AddWithValue("@pid", newParentId ?? 0L);
            resolvedOrden = Convert.ToInt32(await maxOrd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE material_group_nodes SET parent_id = @pid, orden = @ord WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", nodeId);
        cmd.Parameters.AddWithValue("@pid", (object?)newParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ord", resolvedOrden);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, usuario, "NODO_GRUPO_MOVER", "GRUPO_MATERIAL",
            nodeId.ToString(CultureInfo.InvariantCulture),
            $"Nodo #{nodeId} movido hacia padre #{newParentId?.ToString(CultureInfo.InvariantCulture) ?? "RAIZ"}.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task SwapMaterialNodeOrderAsync(long nodeId, bool moveUp, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        IReadOnlyList<MaterialGroupNode> all = await GetMaterialNodesAsync(ct).ConfigureAwait(false);
        MaterialGroupNode? target = all.FirstOrDefault(n => n.Id == nodeId);
        if (target is null)
        {
            return;
        }

        List<MaterialGroupNode> siblings = [.. all
            .Where(n => n.ParentId == target.ParentId)
            .OrderBy(n => n.Orden)
            .ThenBy(n => n.Nombre, StringComparer.OrdinalIgnoreCase)];

        int idx = siblings.FindIndex(n => n.Id == nodeId);
        int swapIdx = moveUp ? idx - 1 : idx + 1;
        if (idx < 0 || swapIdx < 0 || swapIdx >= siblings.Count)
        {
            return;
        }

        MaterialGroupNode other = siblings[swapIdx];
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        // Normaliza orden de todos los hermanos e intercambia posiciones
        for (int i = 0; i < siblings.Count; i++)
        {
            int newOrd = i + 1;
            if (i == idx) newOrd = swapIdx + 1;
            else if (i == swapIdx) newOrd = idx + 1;

            await using SqliteCommand upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = "UPDATE material_group_nodes SET orden = @o WHERE id = @id";
            upd.Parameters.AddWithValue("@o", newOrd);
            upd.Parameters.AddWithValue("@id", siblings[i].Id);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, usuario, "NODO_GRUPO_REORDENAR", "GRUPO_MATERIAL",
            nodeId.ToString(CultureInfo.InvariantCulture),
            $"Reordenado '{target.Nombre}' {(moveUp ? "arriba" : "abajo")} respecto a '{other.Nombre}'.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteMaterialNodeAsync(long nodeId, bool deleteSubtree, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        if (!deleteSubtree)
        {
            await using SqliteCommand reparent = conn.CreateCommand();
            reparent.Transaction = tx;
            reparent.CommandText = """
                UPDATE material_group_nodes
                SET parent_id = (SELECT parent_id FROM material_group_nodes WHERE id = @id)
                WHERE parent_id = @id
                """;
            reparent.Parameters.AddWithValue("@id", nodeId);
            await reparent.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using SqliteCommand del = conn.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM material_group_nodes WHERE id = @id";
        del.Parameters.AddWithValue("@id", nodeId);
        await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, usuario, "NODO_GRUPO_ELIMINAR", "GRUPO_MATERIAL",
            nodeId.ToString(CultureInfo.InvariantCulture),
            $"Eliminado nodo #{nodeId} (subárbol={deleteSubtree}).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<(int Imported, int Duplicates)> ImportPesajesAsync(
        IEnumerable<Pesaje> pesajes,
        Planta planta,
        string usuario,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);

        HashSet<long> existingFolios = [];
        HashSet<(long Folio, TimeOnly Hora)> existingThno = [];

        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

        // Proyección liviana indexada (evita hidratar las 26 columnas de decenas de miles de registros)
        await using (SqliteCommand keysCmd = conn.CreateCommand())
        {
            keysCmd.CommandText = "SELECT folio, hora_ingreso FROM pesajes WHERE planta = @planta";
            keysCmd.Parameters.AddWithValue("@planta", (int)planta);
            await using SqliteDataReader rd = await keysCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                long folio = rd.GetInt64(0);
                if (planta == Planta.Talcahuano)
                {
                    TimeOnly hora = TimeOnly.ParseExact(rd.GetString(1), "HH:mm:ss", CultureInfo.InvariantCulture);
                    existingThno.Add((folio, hora));
                }
                else
                {
                    existingFolios.Add(folio);
                }
            }
        }

        int imported = 0;
        int duplicates = 0;

        await using SqliteTransaction tx = conn.BeginTransaction();
        await using SqliteCommand insertCmd = CreateInsertPesajeCommand(conn, tx);

        foreach (Pesaje p in pesajes)
        {
            bool isDup = planta == Planta.Talcahuano
                ? existingThno.Contains((p.Folio, p.HoraIngreso))
                : existingFolios.Contains(p.Folio);

            if (isDup)
            {
                duplicates++;
                continue;
            }

            if (planta == Planta.Talcahuano)
            {
                existingThno.Add((p.Folio, p.HoraIngreso));
            }
            else
            {
                existingFolios.Add(p.Folio);
            }

            BindInsertPesajeParameters(insertCmd, p);
            await insertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            imported++;
        }

        await AddAuditTxAsync(conn, tx, usuario, "IMPORTACION_PESAJES", planta.ToDisplayName(),
            planta.ToString(), $"Importados {imported} pesajes ({duplicates} duplicados omitidos).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return (imported, duplicates);
    }

    public async Task UpsertClientesAsync(IEnumerable<Cliente> clientes, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        int count = 0;
        foreach (Cliente c in clientes)
        {
            if (string.IsNullOrWhiteSpace(c.Rut))
            {
                continue;
            }

            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO clientes(rut, digito, nombre, razon_social, giro, comuna, ciudad, direccion, telefono, planta_asociada)
                VALUES(@r, @d, @n, @rs, @g, @com, @ci, @dir, @tel, @pl)
                ON CONFLICT(rut) DO UPDATE SET
                    digito = CASE WHEN excluded.digito <> '' THEN excluded.digito ELSE clientes.digito END,
                    nombre = excluded.nombre,
                    razon_social = excluded.razon_social,
                    giro = CASE WHEN excluded.giro <> '' THEN excluded.giro ELSE clientes.giro END,
                    comuna = CASE WHEN excluded.comuna <> '' THEN excluded.comuna ELSE clientes.comuna END,
                    ciudad = CASE WHEN excluded.ciudad <> '' THEN excluded.ciudad ELSE clientes.ciudad END,
                    direccion = CASE WHEN excluded.direccion <> '' THEN excluded.direccion ELSE clientes.direccion END,
                    telefono = CASE WHEN excluded.telefono <> '' THEN excluded.telefono ELSE clientes.telefono END
                """;
            cmd.Parameters.AddWithValue("@r", c.Rut.Trim());
            cmd.Parameters.AddWithValue("@d", c.Digito.Trim());
            cmd.Parameters.AddWithValue("@n", c.Nombre.Trim());
            cmd.Parameters.AddWithValue("@rs", string.IsNullOrWhiteSpace(c.RazonSocial) ? c.Nombre.Trim() : c.RazonSocial.Trim());
            cmd.Parameters.AddWithValue("@g", c.Giro.Trim());
            cmd.Parameters.AddWithValue("@com", c.Comuna.Trim());
            cmd.Parameters.AddWithValue("@ci", c.Ciudad.Trim());
            cmd.Parameters.AddWithValue("@dir", c.Direccion.Trim());
            cmd.Parameters.AddWithValue("@tel", c.Telefono.Trim());
            cmd.Parameters.AddWithValue("@pl", c.PlantaAsociada.HasValue ? (int)c.PlantaAsociada.Value : DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            count++;
        }

        await AddAuditTxAsync(conn, tx, usuario, "SINCRONIZACION_CLIENTES", "CLIENTES", count.ToString(CultureInfo.InvariantCulture),
            $"Actualizados {count} clientes/proveedores.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task UpsertProductosAsync(IEnumerable<Producto> productos, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        int count = 0;
        foreach (Producto p in productos)
        {
            if (string.IsNullOrWhiteSpace(p.Codigo))
            {
                continue;
            }

            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO productos(codigo, descripcion, grupo, unidad_medida)
                VALUES(@c, @d, @g, @u)
                ON CONFLICT(codigo) DO UPDATE SET
                    descripcion = excluded.descripcion,
                    grupo = CASE WHEN excluded.grupo <> 'GENERAL' THEN excluded.grupo ELSE productos.grupo END
                """;
            cmd.Parameters.AddWithValue("@c", p.Codigo.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@d", p.Descripcion.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@g", string.IsNullOrWhiteSpace(p.Grupo) ? "GENERAL" : p.Grupo.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@u", p.UnidadMedida);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            count++;
        }

        await AddAuditTxAsync(conn, tx, usuario, "SINCRONIZACION_PRODUCTOS", "PRODUCTOS", count.ToString(CultureInfo.InvariantCulture),
            $"Actualizados {count} productos.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> PurgeAllPesajesAsync(string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM pesajes";
        int totalDeleted = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, usuario, "PURGA_TABLAS", "SISTEMA",
            totalDeleted.ToString(CultureInfo.InvariantCulture),
            $"Purga confirmada ejecutada ({totalDeleted} pesajes eliminados).", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return totalDeleted;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(
        int limit = 500,
        string? accion = null,
        string? usuario = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();

        List<string> filters = [];
        if (!string.IsNullOrWhiteSpace(accion))
        {
            filters.Add("accion = @a");
            cmd.Parameters.AddWithValue("@a", accion.Trim());
        }

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            filters.Add("LOWER(usuario) LIKE @u");
            cmd.Parameters.AddWithValue("@u", $"%{usuario.Trim().ToLowerInvariant()}%");
        }

        string clause = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty;
        cmd.CommandText = $"""
            SELECT id, timestamp_utc, usuario, accion, entidad, registro_id, detalle
            FROM audit_log
            {clause}
            ORDER BY id DESC
            LIMIT @lim
            """;
        cmd.Parameters.AddWithValue("@lim", Math.Clamp(limit, 1, 2000));

        List<AuditLogEntry> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new AuditLogEntry
            {
                Id = rd.GetInt64(0),
                TimestampUtc = DateTime.Parse(rd.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Usuario = rd.GetString(2),
                Accion = rd.GetString(3),
                Entidad = rd.GetString(4),
                RegistroId = rd.GetString(5),
                Detalle = rd.GetString(6)
            });
        }

        return list;
    }

    public async Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await AddAuditTxAsync(conn, null, entry.Usuario, entry.Accion, entry.Entidad, entry.RegistroId, entry.Detalle, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImportacionHistorial>> GetImportHistoryAsync(int limit = 200, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, timestamp_utc, usuario, tipo_origen, origen_detalle, planta_destino, destino_detalle,
                   rango_filtro, leidos_origen, importados_nuevos, duplicados_omitidos,
                   clientes_sincronizados, productos_sincronizados, kilos_importados, estado, observacion
            FROM import_history
            ORDER BY id DESC
            LIMIT @lim
            """;
        cmd.Parameters.AddWithValue("@lim", Math.Clamp(limit, 1, 1000));

        List<ImportacionHistorial> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new ImportacionHistorial
            {
                Id = rd.GetInt64(0),
                TimestampUtc = DateTime.Parse(rd.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Usuario = rd.GetString(2),
                TipoOrigen = rd.GetString(3),
                OrigenDetalle = rd.GetString(4),
                PlantaDestino = rd.GetString(5),
                DestinoDetalle = rd.GetString(6),
                RangoFiltro = rd.GetString(7),
                LeidosOrigen = rd.GetInt32(8),
                ImportadosNuevos = rd.GetInt32(9),
                DuplicadosOmitidos = rd.GetInt32(10),
                ClientesSincronizados = rd.GetInt32(11),
                ProductosSincronizados = rd.GetInt32(12),
                KilosImportados = rd.GetInt64(13),
                Estado = rd.GetString(14),
                Observacion = rd.GetString(15)
            });
        }

        return list;
    }

    public async Task AddImportHistoryAsync(ImportacionHistorial entry, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO import_history(
                timestamp_utc, usuario, tipo_origen, origen_detalle, planta_destino, destino_detalle,
                rango_filtro, leidos_origen, importados_nuevos, duplicados_omitidos,
                clientes_sincronizados, productos_sincronizados, kilos_importados, estado, observacion)
            VALUES(@ts, @u, @to, @od, @pd, @dd, @rf, @le, @im, @du, @cl, @pr, @kg, @es, @ob)
            """;
        cmd.Parameters.AddWithValue("@ts", entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@u", entry.Usuario);
        cmd.Parameters.AddWithValue("@to", entry.TipoOrigen);
        cmd.Parameters.AddWithValue("@od", entry.OrigenDetalle);
        cmd.Parameters.AddWithValue("@pd", entry.PlantaDestino);
        cmd.Parameters.AddWithValue("@dd", entry.DestinoDetalle);
        cmd.Parameters.AddWithValue("@rf", entry.RangoFiltro);
        cmd.Parameters.AddWithValue("@le", entry.LeidosOrigen);
        cmd.Parameters.AddWithValue("@im", entry.ImportadosNuevos);
        cmd.Parameters.AddWithValue("@du", entry.DuplicadosOmitidos);
        cmd.Parameters.AddWithValue("@cl", entry.ClientesSincronizados);
        cmd.Parameters.AddWithValue("@pr", entry.ProductosSincronizados);
        cmd.Parameters.AddWithValue("@kg", entry.KilosImportados);
        cmd.Parameters.AddWithValue("@es", entry.Estado);
        cmd.Parameters.AddWithValue("@ob", entry.Observacion);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PerfilUsuario>> GetPerfilesAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nombre, descripcion, es_sistema, permisos_json FROM role_profiles ORDER BY id";

        List<PerfilUsuario> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            PermisoSistema[] perms = JsonSerializer.Deserialize<PermisoSistema[]>(rd.GetString(4)) ?? [];
            list.Add(new PerfilUsuario
            {
                Id = rd.GetInt64(0),
                Nombre = rd.GetString(1),
                Descripcion = rd.GetString(2),
                EsSistema = rd.GetInt32(3) == 1,
                Permisos = perms
            });
        }

        return list;
    }

    public async Task<PerfilUsuario?> GetPerfilByIdAsync(long id, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nombre, descripcion, es_sistema, permisos_json FROM role_profiles WHERE id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", id);

        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        PermisoSistema[] perms = JsonSerializer.Deserialize<PermisoSistema[]>(rd.GetString(4)) ?? [];
        return new PerfilUsuario
        {
            Id = rd.GetInt64(0),
            Nombre = rd.GetString(1),
            Descripcion = rd.GetString(2),
            EsSistema = rd.GetInt32(3) == 1,
            Permisos = perms
        };
    }

    public async Task<PerfilUsuario> SavePerfilAsync(PerfilUsuario perfil, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        string permJson = JsonSerializer.Serialize(perfil.Permisos);
        long id = perfil.Id;
        if (id == 0)
        {
            await using SqliteCommand ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO role_profiles(nombre, descripcion, es_sistema, permisos_json)
                VALUES(@n, @d, 0, @p);
                SELECT last_insert_rowid();
                """;
            ins.Parameters.AddWithValue("@n", perfil.Nombre.Trim());
            ins.Parameters.AddWithValue("@d", perfil.Descripcion.Trim());
            ins.Parameters.AddWithValue("@p", permJson);
            id = Convert.ToInt64(await ins.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
        else
        {
            await using SqliteCommand upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE role_profiles
                SET nombre = @n, descripcion = @d, permisos_json = @p
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("@id", id);
            upd.Parameters.AddWithValue("@n", perfil.Nombre.Trim());
            upd.Parameters.AddWithValue("@d", perfil.Descripcion.Trim());
            upd.Parameters.AddWithValue("@p", permJson);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await AddAuditTxAsync(conn, tx, actor, perfil.Id == 0 ? "PERFIL_CREAR" : "PERFIL_EDITAR", "SEGURIDAD",
            id.ToString(CultureInfo.InvariantCulture),
            $"Perfil '{perfil.Nombre}' guardado con {perfil.Permisos.Count} permisos.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new PerfilUsuario
        {
            Id = id,
            Nombre = perfil.Nombre.Trim(),
            Descripcion = perfil.Descripcion.Trim(),
            EsSistema = perfil.EsSistema,
            Permisos = perfil.Permisos
        };
    }

    public async Task DeletePerfilAsync(long id, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await using (SqliteCommand chkSys = conn.CreateCommand())
        {
            chkSys.Transaction = tx;
            chkSys.CommandText = "SELECT es_sistema FROM role_profiles WHERE id = @id";
            chkSys.Parameters.AddWithValue("@id", id);
            object? val = await chkSys.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (val is not null && Convert.ToInt32(val, CultureInfo.InvariantCulture) == 1)
            {
                throw new InvalidOperationException("No se puede eliminar el perfil principal Administrador.");
            }
        }

        await using (SqliteCommand chkUsers = conn.CreateCommand())
        {
            chkUsers.Transaction = tx;
            chkUsers.CommandText = "SELECT COUNT(*) FROM usuarios WHERE perfil_id = @id";
            chkUsers.Parameters.AddWithValue("@id", id);
            long usersCount = Convert.ToInt64(await chkUsers.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (usersCount > 0)
            {
                throw new InvalidOperationException($"El perfil aún tiene {usersCount} usuario(s) asignado(s). Reasígnelos antes de eliminar.");
            }
        }

        await using SqliteCommand del = conn.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM role_profiles WHERE id = @id";
        del.Parameters.AddWithValue("@id", id);
        await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, "PERFIL_ELIMINAR", "SEGURIDAD", id.ToString(CultureInfo.InvariantCulture),
            $"Eliminado perfil #{id}.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Usuario>> GetUsuariosAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.nombre_usuario, u.nombre_completo, u.perfil_id, rp.nombre,
                   u.password_hash, u.password_salt, u.activo, u.intentos_fallidos,
                   u.bloqueado_hasta_utc, u.permisos_json, u.creado_en_utc
            FROM usuarios u
            JOIN role_profiles rp ON rp.id = u.perfil_id
            ORDER BY u.id
            """;

        List<Usuario> users = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            users.Add(MapUsuario(rd));
        }

        return users;
    }

    public async Task<Usuario?> GetUsuarioByNombreAsync(string nombreUsuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.nombre_usuario, u.nombre_completo, u.perfil_id, rp.nombre,
                   u.password_hash, u.password_salt, u.activo, u.intentos_fallidos,
                   u.bloqueado_hasta_utc, u.permisos_json, u.creado_en_utc
            FROM usuarios u
            JOIN role_profiles rp ON rp.id = u.perfil_id
            WHERE LOWER(u.nombre_usuario) = LOWER(@u)
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@u", nombreUsuario.Trim());

        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false) ? MapUsuario(rd) : null;
    }

    public async Task SaveUsuarioAsync(Usuario usuario, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        string permJson = JsonSerializer.Serialize(usuario.PermisosPersonalizados);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        if (usuario.Id > 0)
        {
            await EnsureNotRemovingLastActiveAdminAsync(conn, tx, usuario.Id, usuario.PerfilId, usuario.Activo, ct).ConfigureAwait(false);

            cmd.CommandText = """
                UPDATE usuarios
                SET nombre_usuario = @u, nombre_completo = @nc, perfil_id = @pid,
                    password_hash = @h, password_salt = @s, activo = @a,
                    intentos_fallidos = @ifal, bloqueado_hasta_utc = @bloq, permisos_json = @pj
                WHERE id = @id
                """;
            cmd.Parameters.AddWithValue("@id", usuario.Id);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO usuarios(nombre_usuario, nombre_completo, perfil_id, password_hash, password_salt, activo, intentos_fallidos, bloqueado_hasta_utc, permisos_json, creado_en_utc)
                VALUES(@u, @nc, @pid, @h, @s, @a, @ifal, @bloq, @pj, @ts)
                """;
            cmd.Parameters.AddWithValue("@ts", usuario.CreadoEnUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        cmd.Parameters.AddWithValue("@u", usuario.NombreUsuario.Trim());
        cmd.Parameters.AddWithValue("@nc", usuario.NombreCompleto.Trim());
        cmd.Parameters.AddWithValue("@pid", usuario.PerfilId);
        cmd.Parameters.AddWithValue("@h", usuario.PasswordHash);
        cmd.Parameters.AddWithValue("@s", usuario.PasswordSalt);
        cmd.Parameters.AddWithValue("@a", usuario.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@ifal", usuario.IntentosFallidos);
        cmd.Parameters.AddWithValue("@bloq", usuario.BloqueadoHastaUtc?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pj", permJson);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (!string.Equals(actor, "SISTEMA", StringComparison.OrdinalIgnoreCase))
        {
            await AddAuditTxAsync(conn, tx, actor, usuario.Id > 0 ? "ACTUALIZACION_USUARIO" : "CREACION_USUARIO",
                "SEGURIDAD", usuario.NombreUsuario, $"Usuario '{usuario.NombreUsuario}' (Perfil #{usuario.PerfilId}, Activo={usuario.Activo}).", ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteUsuarioAsync(long id, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = conn.BeginTransaction();

        await EnsureNotRemovingLastActiveAdminAsync(conn, tx, id, newPerfilId: 0, newActivo: false, ct).ConfigureAwait(false);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM usuarios WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await AddAuditTxAsync(conn, tx, actor, "ELIMINACION_USUARIO", "SEGURIDAD",
            id.ToString(CultureInfo.InvariantCulture), $"Eliminada cuenta usuario ID {id}.", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<string> BackupDatabaseAsync(string destinationPath, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string destCs = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Pooling = false
        }.ToString();

        await using (SqliteConnection source = await OpenConnectionAsync(ct).ConfigureAwait(false))
        await using (SqliteConnection dest = new(destCs))
        {
            await dest.OpenAsync(ct).ConfigureAwait(false);
            source.BackupDatabase(dest);

            await using SqliteCommand checkCmd = dest.CreateCommand();
            checkCmd.CommandText = "PRAGMA quick_check;";
            string check = Convert.ToString(await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.Equals(check, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"El archivo de respaldo generado no superó PRAGMA quick_check ({check}).");
            }
        }

        return destinationPath;
    }

    public async Task<DatabaseSummaryMetrics> GetDatabaseSummaryMetricsAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

        int totalPesajes = 0;
        long totalKilos = 0;
        DateOnly? primeraFecha = null;
        DateOnly? ultimaFecha = null;
        int totalClientes = 0;
        int totalProductos = 0;
        int totalImportaciones = 0;

        await using (SqliteCommand aggCmd = conn.CreateCommand())
        {
            aggCmd.CommandText = """
                SELECT
                    COUNT(*),
                    COALESCE(SUM(peso_neto), 0),
                    MIN(fecha_ingreso),
                    MAX(fecha_ingreso),
                    (SELECT COUNT(*) FROM clientes),
                    (SELECT COUNT(*) FROM productos),
                    (SELECT COUNT(*) FROM import_history)
                FROM pesajes
                """;
            await using SqliteDataReader rd = await aggCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                totalPesajes = rd.GetInt32(0);
                totalKilos = rd.GetInt64(1);
                if (!rd.IsDBNull(2) && DateOnly.TryParseExact(rd.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly dMin))
                {
                    primeraFecha = dMin;
                }

                if (!rd.IsDBNull(3) && DateOnly.TryParseExact(rd.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly dMax))
                {
                    ultimaFecha = dMax;
                }

                totalClientes = rd.GetInt32(4);
                totalProductos = rd.GetInt32(5);
                totalImportaciones = rd.GetInt32(6);
            }
        }

        DateTime? ultimaImportacionUtc = null;
        string ultimoOrigenResumen = "Sin importaciones registradas aún";
        await using (SqliteCommand lastCmd = conn.CreateCommand())
        {
            lastCmd.CommandText = "SELECT timestamp_utc, tipo_origen, origen_detalle FROM import_history ORDER BY id DESC LIMIT 1";
            await using SqliteDataReader rd = await lastCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                ultimaImportacionUtc = DateTime.Parse(rd.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                ultimoOrigenResumen = $"{rd.GetString(1)}: {rd.GetString(2)}";
            }
        }

        return new DatabaseSummaryMetrics(
            totalPesajes,
            totalKilos,
            totalClientes,
            totalProductos,
            totalImportaciones,
            primeraFecha,
            ultimaFecha,
            ultimaImportacionUtc,
            ultimoOrigenResumen);
    }

    public async Task<DatabaseIntegrityReport> VerifyIntegrityAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

        string quickCheck;
        await using (SqliteCommand qc = conn.CreateCommand())
        {
            qc.CommandText = "PRAGMA quick_check;";
            quickCheck = Convert.ToString(await qc.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? "unknown";
        }

        int fkViolations = 0;
        await using (SqliteCommand fkc = conn.CreateCommand())
        {
            fkc.CommandText = "PRAGMA foreign_key_check;";
            await using SqliteDataReader rd = await fkc.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                fkViolations++;
            }
        }

        int schemaVer;
        await using (SqliteCommand sv = conn.CreateCommand())
        {
            sv.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
            schemaVer = Convert.ToInt32(await sv.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        long pesajesCount;
        await using (SqliteCommand pc = conn.CreateCommand())
        {
            pc.CommandText = "SELECT COUNT(*) FROM pesajes;";
            pesajesCount = Convert.ToInt64(await pc.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        bool healthy = string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase) && fkViolations == 0;
        string summary = healthy
            ? $"Integridad SQLite OK (quick_check=ok, fk_errors=0, schema=v{schemaVer}, pesajes={pesajesCount:N0})."
            : $"ALERTA DE INTEGRIDAD SQLite: quick_check={quickCheck}, fk_errors={fkViolations}, schema=v{schemaVer}.";

        return new DatabaseIntegrityReport(healthy, quickCheck, fkViolations, schemaVer, pesajesCount, summary);
    }

    private static async Task EnsureNotRemovingLastActiveAdminAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        long targetUserId,
        long newPerfilId,
        bool newActivo,
        CancellationToken ct)
    {
        bool targetIsCurrentlyActiveAdmin = false;
        await using (SqliteCommand chkTarget = conn.CreateCommand())
        {
            chkTarget.Transaction = tx;
            chkTarget.CommandText = """
                SELECT COUNT(*)
                FROM usuarios u
                JOIN role_profiles rp ON rp.id = u.perfil_id
                WHERE u.id = @id AND u.activo = 1 AND rp.es_sistema = 1
                """;
            chkTarget.Parameters.AddWithValue("@id", targetUserId);
            targetIsCurrentlyActiveAdmin = Convert.ToInt64(await chkTarget.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture) > 0;
        }

        if (!targetIsCurrentlyActiveAdmin)
        {
            return;
        }

        bool remainsAdmin = false;
        if (newActivo && newPerfilId > 0)
        {
            await using SqliteCommand chkNewRole = conn.CreateCommand();
            chkNewRole.Transaction = tx;
            chkNewRole.CommandText = "SELECT COALESCE(es_sistema, 0) FROM role_profiles WHERE id = @pid";
            chkNewRole.Parameters.AddWithValue("@pid", newPerfilId);
            remainsAdmin = Convert.ToInt64(await chkNewRole.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L, CultureInfo.InvariantCulture) == 1;
        }

        if (remainsAdmin)
        {
            return;
        }

        await using SqliteCommand chkOthers = conn.CreateCommand();
        chkOthers.Transaction = tx;
        chkOthers.CommandText = """
            SELECT COUNT(*)
            FROM usuarios u
            JOIN role_profiles rp ON rp.id = u.perfil_id
            WHERE u.id <> @id AND u.activo = 1 AND rp.es_sistema = 1
            """;
        chkOthers.Parameters.AddWithValue("@id", targetUserId);
        long otherActiveAdmins = Convert.ToInt64(await chkOthers.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (otherActiveAdmins == 0)
        {
            throw new InvalidOperationException("No se puede eliminar, desactivar ni degradar al último administrador activo del sistema.");
        }
    }

    private static Usuario MapUsuario(SqliteDataReader rd)
    {
        PermisoSistema[] customPerms = JsonSerializer.Deserialize<PermisoSistema[]>(rd.GetString(10)) ?? [];
        return new Usuario
        {
            Id = rd.GetInt64(0),
            NombreUsuario = rd.GetString(1),
            NombreCompleto = rd.GetString(2),
            PerfilId = rd.GetInt64(3),
            NombrePerfil = rd.GetString(4),
            PasswordHash = rd.GetString(5),
            PasswordSalt = rd.GetString(6),
            Activo = rd.GetInt32(7) == 1,
            IntentosFallidos = rd.GetInt32(8),
            BloqueadoHastaUtc = rd.IsDBNull(9) ? null : DateTime.Parse(rd.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            PermisosPersonalizados = customPerms,
            CreadoEnUtc = DateTime.Parse(rd.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    private static SqliteCommand CreateInsertPesajeCommand(SqliteConnection conn, SqliteTransaction tx)
    {
        SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO pesajes(folio, planta, tipo_pesaje, patente, acoplado, peso_bruto, peso_tara, peso_neto,
                                cod_producto, nom_producto, rut_cliente, nom_cliente, origen, destino,
                                guia_despacho, chofer, transportista, fecha_ingreso, hora_ingreso, fecha_salida,
                                hora_salida, observacion, grupo_material, es_traslado, ciudad_cliente)
            VALUES(@folio, @pl, @tp, @pat, @aco, @br, @ta, @ne, @cp, @np, @rc, @nc, @or, @de, @gu, @ch, @tr,
                   @fi, @hi, @fs, @hs, @ob, @gm, @et, @ci)
            """;
        cmd.Parameters.Add("@folio", SqliteType.Integer);
        cmd.Parameters.Add("@pl", SqliteType.Integer);
        cmd.Parameters.Add("@tp", SqliteType.Integer);
        cmd.Parameters.Add("@pat", SqliteType.Text);
        cmd.Parameters.Add("@aco", SqliteType.Text);
        cmd.Parameters.Add("@br", SqliteType.Integer);
        cmd.Parameters.Add("@ta", SqliteType.Integer);
        cmd.Parameters.Add("@ne", SqliteType.Integer);
        cmd.Parameters.Add("@cp", SqliteType.Text);
        cmd.Parameters.Add("@np", SqliteType.Text);
        cmd.Parameters.Add("@rc", SqliteType.Text);
        cmd.Parameters.Add("@nc", SqliteType.Text);
        cmd.Parameters.Add("@or", SqliteType.Text);
        cmd.Parameters.Add("@de", SqliteType.Text);
        cmd.Parameters.Add("@gu", SqliteType.Text);
        cmd.Parameters.Add("@ch", SqliteType.Text);
        cmd.Parameters.Add("@tr", SqliteType.Text);
        cmd.Parameters.Add("@fi", SqliteType.Text);
        cmd.Parameters.Add("@hi", SqliteType.Text);
        cmd.Parameters.Add("@fs", SqliteType.Text);
        cmd.Parameters.Add("@hs", SqliteType.Text);
        cmd.Parameters.Add("@ob", SqliteType.Text);
        cmd.Parameters.Add("@gm", SqliteType.Text);
        cmd.Parameters.Add("@et", SqliteType.Integer);
        cmd.Parameters.Add("@ci", SqliteType.Text);
        cmd.Prepare();
        return cmd;
    }

    private static void BindInsertPesajeParameters(SqliteCommand cmd, Pesaje p)
    {
        cmd.Parameters["@folio"].Value = p.Folio;
        cmd.Parameters["@pl"].Value = (int)p.Planta;
        cmd.Parameters["@tp"].Value = (int)p.TipoPesaje;
        cmd.Parameters["@pat"].Value = p.Patente;
        cmd.Parameters["@aco"].Value = p.Acoplado;
        cmd.Parameters["@br"].Value = p.PesoBruto;
        cmd.Parameters["@ta"].Value = p.PesoTara;
        cmd.Parameters["@ne"].Value = p.PesoNeto;
        cmd.Parameters["@cp"].Value = p.CodProducto;
        cmd.Parameters["@np"].Value = p.NomProducto;
        cmd.Parameters["@rc"].Value = p.RutCliente;
        cmd.Parameters["@nc"].Value = p.NomCliente;
        cmd.Parameters["@or"].Value = p.Origen;
        cmd.Parameters["@de"].Value = p.Destino;
        cmd.Parameters["@gu"].Value = p.GuiaDespacho;
        cmd.Parameters["@ch"].Value = p.Chofer;
        cmd.Parameters["@tr"].Value = p.Transportista;
        cmd.Parameters["@fi"].Value = p.FechaIngreso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        cmd.Parameters["@hi"].Value = p.HoraIngreso.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        cmd.Parameters["@fs"].Value = p.FechaSalida.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        cmd.Parameters["@hs"].Value = p.HoraSalida.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        cmd.Parameters["@ob"].Value = p.Observacion;
        cmd.Parameters["@gm"].Value = p.GrupoMaterial;
        cmd.Parameters["@et"].Value = p.EsTraslado ? 1 : 0;
        cmd.Parameters["@ci"].Value = (object?)p.CiudadCliente ?? DBNull.Value;
    }

    private static async Task AddAuditTxAsync(
        SqliteConnection conn,
        SqliteTransaction? tx,
        string usuario,
        string accion,
        string entidad,
        string registroId,
        string detalle,
        CancellationToken ct)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO audit_log(timestamp_utc, usuario, accion, entidad, registro_id, detalle)
            VALUES(@ts, @u, @a, @e, @r, @d)
            """;
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@u", usuario);
        cmd.Parameters.AddWithValue("@a", accion);
        cmd.Parameters.AddWithValue("@e", entidad);
        cmd.Parameters.AddWithValue("@r", registroId);
        cmd.Parameters.AddWithValue("@d", detalle);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        try
        {
            using SqliteConnection poolKey = new(_connectionString);
            SqliteConnection.ClearPool(poolKey);
        }
        catch
        {
            // Liberación best-effort del pool SQLite
        }

        _initLock.Dispose();
    }
}
