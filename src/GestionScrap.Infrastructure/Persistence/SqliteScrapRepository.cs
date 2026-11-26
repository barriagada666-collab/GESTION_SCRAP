using System.Globalization;
using System.Text.Json;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using Microsoft.Data.Sqlite;

namespace GestionScrap.Infrastructure.Persistence;

/// <summary>
/// Repositorio central en SQLite (WAL mode, consultas parametrizadas, índices compuestos).
/// Elimina el riesgo de corrupción e inyección SQL del motor legacy Access/Jet.
/// </summary>
public sealed class SqliteScrapRepository : IScrapRepository, IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqliteScrapRepository(string dbPath)
    {
        _dbPath = dbPath;
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
        using SqliteCommand pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
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
            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;

                CREATE TABLE IF NOT EXISTS productos (
                    codigo TEXT PRIMARY KEY,
                    descripcion TEXT NOT NULL,
                    grupo TEXT NOT NULL,
                    unidad_medida TEXT NOT NULL DEFAULT 'KG'
                );

                CREATE TABLE IF NOT EXISTS clientes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    rut TEXT NOT NULL,
                    digito TEXT NOT NULL DEFAULT '',
                    nombre TEXT NOT NULL,
                    razon_social TEXT NOT NULL,
                    ciudad TEXT NOT NULL DEFAULT '',
                    direccion TEXT NOT NULL DEFAULT '',
                    planta_asociada INTEGER NOT NULL DEFAULT 0,
                    UNIQUE(rut, planta_asociada)
                );

                CREATE TABLE IF NOT EXISTS pesajes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    folio INTEGER NOT NULL,
                    planta INTEGER NOT NULL,
                    tipo_pesaje INTEGER NOT NULL,
                    patente TEXT NOT NULL DEFAULT '',
                    peso_bruto INTEGER NOT NULL DEFAULT 0,
                    peso_tara INTEGER NOT NULL DEFAULT 0,
                    peso_neto INTEGER NOT NULL DEFAULT 0,
                    cod_producto TEXT NOT NULL DEFAULT '',
                    nom_producto TEXT NOT NULL DEFAULT '',
                    rut_cliente TEXT NOT NULL DEFAULT '',
                    nom_cliente TEXT NOT NULL DEFAULT '',
                    origen TEXT NOT NULL DEFAULT '',
                    destino TEXT NOT NULL DEFAULT '',
                    guia_despacho TEXT NOT NULL DEFAULT '',
                    fecha_ingreso TEXT NOT NULL,
                    hora_ingreso TEXT NOT NULL DEFAULT '00:00:00',
                    fecha_salida TEXT NOT NULL,
                    hora_salida TEXT NOT NULL DEFAULT '00:00:00',
                    observacion TEXT NOT NULL DEFAULT '',
                    grupo_material TEXT NOT NULL DEFAULT 'GENERAL',
                    es_traslado INTEGER NOT NULL DEFAULT 0,
                    ciudad_cliente TEXT NULL,
                    UNIQUE(planta, folio, hora_ingreso)
                );

                CREATE INDEX IF NOT EXISTS idx_pesajes_fecha_planta ON pesajes(fecha_ingreso, planta);
                CREATE INDEX IF NOT EXISTS idx_pesajes_folio ON pesajes(folio);
                CREATE INDEX IF NOT EXISTS idx_pesajes_rut ON pesajes(rut_cliente);

                CREATE TABLE IF NOT EXISTS precios_proveedor (
                    indice INTEGER PRIMARY KEY AUTOINCREMENT,
                    planta INTEGER NOT NULL,
                    rut TEXT NOT NULL,
                    razon_social TEXT NOT NULL,
                    codigo_producto TEXT NOT NULL,
                    precio NUMERIC NOT NULL,
                    fecha_actualizacion TEXT NOT NULL,
                    UNIQUE(planta, rut, codigo_producto)
                );

                CREATE TABLE IF NOT EXISTS index_precios (
                    planta INTEGER NOT NULL,
                    folio INTEGER NOT NULL,
                    precio NUMERIC NOT NULL,
                    familia TEXT NOT NULL DEFAULT '',
                    actualizado_en TEXT NOT NULL,
                    PRIMARY KEY(planta, folio)
                );

                CREATE TABLE IF NOT EXISTS ultimo_folio_jcramm (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    folio INTEGER NOT NULL,
                    fecha TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS usuarios (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    nombre_usuario TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    nombre_completo TEXT NOT NULL,
                    rol INTEGER NOT NULL,
                    password_hash TEXT NOT NULL,
                    password_salt TEXT NOT NULL,
                    activo INTEGER NOT NULL DEFAULT 1,
                    intentos_fallidos INTEGER NOT NULL DEFAULT 0,
                    bloqueado_hasta_utc TEXT NULL,
                    permisos_json TEXT NOT NULL DEFAULT '[]',
                    creado_en_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS audit_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp_utc TEXT NOT NULL,
                    usuario TEXT NOT NULL,
                    accion TEXT NOT NULL,
                    entidad TEXT NOT NULL,
                    registro_id TEXT NOT NULL DEFAULT '',
                    detalle TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_audit_fecha ON audit_logs(timestamp_utc DESC);
                """;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await SeedInitialSecurityUsersAsync(conn, ct).ConfigureAwait(false);
            await SeedDefaultCatalogAndDemoIfEmptyAsync(conn, ct).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task SeedInitialSecurityUsersAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using SqliteCommand countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM usuarios;";
        long count = (long)(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);
        if (count > 0)
        {
            return;
        }

        (string user, string fullName, RolUsuario rol, string pwd)[] defaults =
        [
            ("admin", "Administrador del Sistema", RolUsuario.Administrador, "Admin@2026"),
            ("supervisor", "Supervisor Control Precios", RolUsuario.Supervisor, "Super@2026"),
            ("operador", "Operador Báscula e Informes", RolUsuario.Operador, "Oper@2026"),
            ("auditor", "Auditoría y Consulta", RolUsuario.Auditor, "Audit@2026")
        ];

        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        foreach (var (u, full, rol, pwd) in defaults)
        {
            (string hash, string salt) = PasswordSecurity.HashPassword(pwd);
            await using SqliteCommand ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO usuarios(nombre_usuario, nombre_completo, rol, password_hash, password_salt, activo, intentos_fallidos, permisos_json, creado_en_utc)
                VALUES(@u, @n, @r, @h, @s, 1, 0, '[]', @c);
                """;
            ins.Parameters.AddWithValue("@u", u);
            ins.Parameters.AddWithValue("@n", full);
            ins.Parameters.AddWithValue("@r", (int)rol);
            ins.Parameters.AddWithValue("@h", hash);
            ins.Parameters.AddWithValue("@s", salt);
            ins.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("O"));
            await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static async Task SeedDefaultCatalogAndDemoIfEmptyAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using SqliteCommand prodCountCmd = conn.CreateCommand();
        prodCountCmd.CommandText = "SELECT COUNT(*) FROM productos;";
        long prodCount = (long)(await prodCountCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);

        if (prodCount == 0)
        {
            await InsertSeedCatalogAsync(conn, ct).ConfigureAwait(false);
        }

        await using SqliteCommand pesajeCountCmd = conn.CreateCommand();
        pesajeCountCmd.CommandText = "SELECT COUNT(*) FROM pesajes;";
        long pesajeCount = (long)(await pesajeCountCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);

        if (pesajeCount == 0)
        {
            await InsertSeedDemoOperationalDataAsync(conn, ct).ConfigureAwait(false);
        }
    }

    private static async Task InsertSeedCatalogAsync(SqliteConnection conn, CancellationToken ct)
    {
        Producto[] productos =
        [
            new() { Codigo = "0000000005", Descripcion = "VENTA DE FIERROS", Grupo = "MAQUILADO" },
            new() { Codigo = "0000000007", Descripcion = "MANGA DE RIEGO", Grupo = "MAQUILADO" },
            new() { Codigo = "0000000008", Descripcion = "MIX SUPERMERCADO MAQUILADO", Grupo = "MAQUILADO" },
            new() { Codigo = "0000000010", Descripcion = "BASURA", Grupo = "BASURA" },
            new() { Codigo = "0000000011", Descripcion = "SCRAP AGRICOLA PARA MAQUILAR", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5300000004", Descripcion = "SCRAP HDPE COMPRADO BLANCO", Grupo = "ALTA" },
            new() { Codigo = "5300000005", Descripcion = "SCRAP HDPE COMPRADO AGROSUPER", Grupo = "ALTA" },
            new() { Codigo = "5300000006", Descripcion = "SCRAP HDPE COMPRADO P/LAVADO", Grupo = "ALTA" },
            new() { Codigo = "5300000007", Descripcion = "SCRAP HDPE TRIPLE LAVADO", Grupo = "ALTA" },
            new() { Codigo = "5300000008", Descripcion = "SCRAP HDPE COMPRADO RIGIDO", Grupo = "ALTA" },
            new() { Codigo = "5300000009", Descripcion = "SCRAP MIX SUPERMERCADO", Grupo = "MIX SUPERMERCADO" },
            new() { Codigo = "5300000010", Descripcion = "SCRAP MIX POST INDUSTRIA", Grupo = "MIX SUPERMERCADO" },
            new() { Codigo = "5301000001", Descripcion = "SCRAP CINTA RIEGO AGRICOLA (SUP)", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5301000002", Descripcion = "SCRAP MIX POST. IND. VIRUTA", Grupo = "MIX SUPERMERCADO" },
            new() { Codigo = "5301000003", Descripcion = "SCRAP COLOR PICADO PREMIUM", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5301000006", Descripcion = "SCRAP COLOR S/C P/L >50 MIC.(SUP)", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5301000007", Descripcion = "SCRAP PE TRANSP. S/CONT.>50 MIC.(RECAM)", Grupo = "RECAM" },
            new() { Codigo = "5301001000", Descripcion = "SCRAP PESQUERA PRE LAVADO COLOR (SUP)", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5301001002", Descripcion = "SCRAP AGRICOLA LAVADO P/L (SUP)", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5301002001", Descripcion = "SCRAP COLOR S/CONTAMINACION P/L (SUP)", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5301002002", Descripcion = "SCRAP PESQUERA TRANSPARENTE PRE LAVADO", Grupo = "RECAM" },
            new() { Codigo = "5301002003", Descripcion = "SCRAP TRANSPARENTE S/CONTAMINACION", Grupo = "RECAM" },
            new() { Codigo = "5301002004", Descripcion = "SCRAP PESQUERA COLOR (SUP)", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5301003001", Descripcion = "SCRAP TRANSPARENTE LIMPIO", Grupo = "RECAM" },
            new() { Codigo = "5301003002", Descripcion = "SCRAP BOBINA BLANCA (RECAM)", Grupo = "RECAM" },
            new() { Codigo = "5301003003", Descripcion = "SCRAP BOBINA TRANSPARENTE (RECAM)", Grupo = "RECAM" },
            new() { Codigo = "5301004000", Descripcion = "SCRAP LD TRANSPARENTE EDELPA (RECAM)", Grupo = "RECAM" },
            new() { Codigo = "5301004001", Descripcion = "SCRAP COMPRADO IMPORTADO", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5302000004", Descripcion = "SCRAP STRETCH TRANSPARENTE", Grupo = "STRETCH" },
            new() { Codigo = "5310000002", Descripcion = "PELLET HDPE COLOR", Grupo = "ALTA" },
            new() { Codigo = "5311000001", Descripcion = "PELLET LDPE COMPRADO", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "5311000007", Descripcion = "PELLET LD STRETCH FILM", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "PRD-01", Descripcion = "POLIETILENO RECICLADO ALTA DENSIDAD (ALTA)", Grupo = "ALTA" },
            new() { Codigo = "PRD-02", Descripcion = "POLIETILENO RECAM TRANSPARENTE INDUSTRIAL", Grupo = "RECAM" },
            new() { Codigo = "PRD-03", Descripcion = "SUPER-BASULIP RESINAS SELECCIONADAS", Grupo = "SUPER-BASULIP" },
            new() { Codigo = "PRD-04", Descripcion = "FILM STRETCH MIX SUPERMERCADO", Grupo = "MIX SUPERMERCADO" },
            new() { Codigo = "PRD-05", Descripcion = "SCRAP DESCARTE POLIETILENO MOLIDO", Grupo = "SCRAP" }
        ];

        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        foreach (Producto p in productos)
        {
            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO productos(codigo, descripcion, grupo, unidad_medida)
                VALUES(@c, @d, @g, @u);
                """;
            cmd.Parameters.AddWithValue("@c", p.Codigo);
            cmd.Parameters.AddWithValue("@d", p.Descripcion);
            cmd.Parameters.AddWithValue("@g", p.Grupo);
            cmd.Parameters.AddWithValue("@u", p.UnidadMedida);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static async Task InsertSeedDemoOperationalDataAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        Cliente[] clientes =
        [
            new() { Rut = "76218764", Digito = "5", Nombre = "Sociedad Agrícola Demostrativa Ltda.", RazonSocial = "Sociedad Agrícola Demostrativa Ltda.", Ciudad = "Valparaíso", PlantaAsociada = Planta.Placilla },
            new() { Rut = "78483600", Digito = "1", Nombre = "Agroindustrial Alimentos Demo Ltda.", RazonSocial = "Agroindustrial Alimentos Demo Ltda.", Ciudad = "Rancagua", PlantaAsociada = Planta.Placilla },
            new() { Rut = "77321450", Digito = "K", Nombre = "Retail Supermercados Santiago S.A.", RazonSocial = "Retail Supermercados Santiago S.A.", Ciudad = "Santiago", PlantaAsociada = Planta.Santiago },
            new() { Rut = "95293000", Digito = "2", Nombre = "Empresas Corporativas Demo S.A.", RazonSocial = "Empresas Corporativas Demo S.A.", Ciudad = "Santiago", PlantaAsociada = Planta.Santiago },
            new() { Rut = "76658930", Digito = "8", Nombre = "Pesquera del Pacífico Demo S.A.", RazonSocial = "Pesquera del Pacífico Demo S.A.", Ciudad = "Talcahuano", PlantaAsociada = Planta.Talcahuano },
            new() { Rut = "15071375", Digito = "7", Nombre = "BODEGA SAN BERNARDO (TRASLADOS)", RazonSocial = "BODEGA SAN BERNARDO (TRASLADOS)", Ciudad = "Santiago", PlantaAsociada = null }
        ];

        foreach (Cliente c in clientes)
        {
            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO clientes(rut, digito, nombre, razon_social, ciudad, direccion, planta_asociada)
                VALUES(@r, @d, @n, @rs, @ci, @di, @p);
                """;
            cmd.Parameters.AddWithValue("@r", c.Rut);
            cmd.Parameters.AddWithValue("@d", c.Digito);
            cmd.Parameters.AddWithValue("@n", c.Nombre);
            cmd.Parameters.AddWithValue("@rs", c.RazonSocial);
            cmd.Parameters.AddWithValue("@ci", c.Ciudad);
            cmd.Parameters.AddWithValue("@di", c.Direccion);
            cmd.Parameters.AddWithValue("@p", c.PlantaAsociada.HasValue ? (int)c.PlantaAsociada.Value : 0);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly mPrev = today.AddMonths(-1);

        Pesaje[] pesajes =
        [
            new() { Folio = 10420, Planta = Planta.Placilla, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "HR-8921", PesoBruto = 28450, PesoTara = 14200, PesoNeto = 14250, CodProducto = "5300000004", NomProducto = "SCRAP HDPE COMPRADO BLANCO", RutCliente = "76218764", NomCliente = "Sociedad Agrícola Demostrativa Ltda.", Origen = "Valparaíso", Destino = "Planta Placilla", GuiaDespacho = "GD-8841", FechaIngreso = today.AddDays(-2), HoraIngreso = new TimeOnly(8, 45), FechaSalida = today.AddDays(-2), HoraSalida = new TimeOnly(9, 20), Observacion = "Descarga conforme", GrupoMaterial = "ALTA" },
            new() { Folio = 10421, Planta = Planta.Placilla, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "KJ-3312", PesoBruto = 31200, PesoTara = 13500, PesoNeto = 17700, CodProducto = "5301003001", NomProducto = "SCRAP TRANSPARENTE LIMPIO", RutCliente = "76218764", NomCliente = "Sociedad Agrícola Demostrativa Ltda.", Origen = "Casablanca", Destino = "Planta Placilla", GuiaDespacho = "GD-8845", FechaIngreso = today.AddDays(-1), HoraIngreso = new TimeOnly(10, 10), FechaSalida = today.AddDays(-1), HoraSalida = new TimeOnly(10, 55), Observacion = "Material limpio", GrupoMaterial = "RECAM" },
            new() { Folio = 10422, Planta = Planta.Placilla, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "PF-4490", PesoBruto = 25800, PesoTara = 12100, PesoNeto = 13700, CodProducto = "5301001002", NomProducto = "SCRAP AGRICOLA LAVADO P/L (SUP)", RutCliente = "78483600", NomCliente = "Agroindustrial Alimentos Demo Ltda.", Origen = "Rancagua", Destino = "Planta Placilla", GuiaDespacho = "AG-9912", FechaIngreso = today, HoraIngreso = new TimeOnly(11, 30), FechaSalida = today, HoraSalida = new TimeOnly(12, 15), Observacion = "Certificado calidad", GrupoMaterial = "SUPER-BASULIP" },
            new() { Folio = 10425, Planta = Planta.Placilla, TipoPesaje = TipoPesaje.MovimientoInterno, Patente = "TR-5520", PesoBruto = 32000, PesoTara = 12500, PesoNeto = 19500, CodProducto = "PRD-05", NomProducto = "SCRAP DESCARTE POLIETILENO MOLIDO", RutCliente = "15071375", NomCliente = "BODEGA SAN BERNARDO (TRASLADOS)", Origen = "SANTIAGO", Destino = "Placilla", GuiaDespacho = "TRAS-401", FechaIngreso = today, HoraIngreso = new TimeOnly(16, 0), FechaSalida = today, HoraSalida = new TimeOnly(16, 45), Observacion = "Traslado interno", GrupoMaterial = "SCRAP", EsTraslado = true },
            new() { Folio = 20550, Planta = Planta.Santiago, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "GG-9012", PesoBruto = 34100, PesoTara = 15100, PesoNeto = 19000, CodProducto = "5300000009", NomProducto = "SCRAP MIX SUPERMERCADO", RutCliente = "77321450", NomCliente = "Retail Supermercados Santiago S.A.", Origen = "San Bernardo", Destino = "Planta Santiago", GuiaDespacho = "CEN-551", FechaIngreso = today.AddDays(-1), HoraIngreso = new TimeOnly(7, 30), FechaSalida = today.AddDays(-1), HoraSalida = new TimeOnly(8, 25), Observacion = "Turno mañana", GrupoMaterial = "MIX SUPERMERCADO" },
            new() { Folio = 20551, Planta = Planta.Santiago, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "WK-1288", PesoBruto = 29500, PesoTara = 13000, PesoNeto = 16500, CodProducto = "5301003002", NomProducto = "SCRAP BOBINA BLANCA (RECAM)", RutCliente = "95293000", NomCliente = "Empresas Corporativas Demo S.A.", Origen = "Santiago", Destino = "Planta Santiago", GuiaDespacho = "EMP-901", FechaIngreso = today, HoraIngreso = new TimeOnly(9, 15), FechaSalida = today, HoraSalida = new TimeOnly(10, 0), Observacion = "Fardos blancos", GrupoMaterial = "RECAM" },
            new() { Folio = 30101, Planta = Planta.Talcahuano, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "TK-4412", PesoBruto = 36200, PesoTara = 14200, PesoNeto = 22000, CodProducto = "5301001000", NomProducto = "SCRAP PESQUERA PRE LAVADO COLOR (SUP)", RutCliente = "76658930", NomCliente = "Pesquera del Pacífico Demo S.A.", Origen = "Talcahuano", Destino = "Bodega Thno", GuiaDespacho = "PESQ-881", FechaIngreso = today, HoraIngreso = new TimeOnly(8, 15), FechaSalida = today, HoraSalida = new TimeOnly(9, 0), Observacion = "Recepción puerto", GrupoMaterial = "SUPER-BASULIP" },
            new() { Folio = 9810, Planta = Planta.Placilla, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "HR-8921", PesoBruto = 27000, PesoTara = 14000, PesoNeto = 13000, CodProducto = "5300000004", NomProducto = "SCRAP HDPE COMPRADO BLANCO", RutCliente = "76218764", NomCliente = "Sociedad Agrícola Demostrativa Ltda.", Origen = "Valparaíso", Destino = "Planta Placilla", GuiaDespacho = "GD-7710", FechaIngreso = mPrev, HoraIngreso = new TimeOnly(9, 0), FechaSalida = mPrev, HoraSalida = new TimeOnly(9, 40), Observacion = "Histórico mes anterior", GrupoMaterial = "ALTA" },
            new() { Folio = 19400, Planta = Planta.Santiago, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "GG-9012", PesoBruto = 31000, PesoTara = 15000, PesoNeto = 16000, CodProducto = "5300000009", NomProducto = "SCRAP MIX SUPERMERCADO", RutCliente = "77321450", NomCliente = "Retail Supermercados Santiago S.A.", Origen = "San Bernardo", Destino = "Planta Santiago", GuiaDespacho = "CEN-4401", FechaIngreso = mPrev, HoraIngreso = new TimeOnly(8, 30), FechaSalida = mPrev, HoraSalida = new TimeOnly(9, 15), Observacion = "Histórico mes anterior", GrupoMaterial = "MIX SUPERMERCADO" }
        ];

        foreach (Pesaje p in pesajes)
        {
            await InsertPesajeInternalAsync(conn, tx, p, ct).ConfigureAwait(false);
        }

        PrecioProveedor[] precios =
        [
            new() { Planta = Planta.Placilla, Rut = "76218764", RazonSocial = "Sociedad Agrícola Demostrativa Ltda.", CodigoProducto = "5300000004", Precio = 340, FechaActualizacion = today },
            new() { Planta = Planta.Placilla, Rut = "76218764", RazonSocial = "Sociedad Agrícola Demostrativa Ltda.", CodigoProducto = "5301003001", Precio = 410, FechaActualizacion = today },
            new() { Planta = Planta.Placilla, Rut = "78483600", RazonSocial = "Agroindustrial Alimentos Demo Ltda.", CodigoProducto = "5301001002", Precio = 520, FechaActualizacion = today },
            new() { Planta = Planta.Santiago, Rut = "77321450", RazonSocial = "Retail Supermercados Santiago S.A.", CodigoProducto = "5300000009", Precio = 290, FechaActualizacion = today },
            new() { Planta = Planta.Santiago, Rut = "95293000", RazonSocial = "Empresas Corporativas Demo S.A.", CodigoProducto = "5301003002", Precio = 430, FechaActualizacion = today }
        ];

        foreach (PrecioProveedor pr in precios)
        {
            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO precios_proveedor(planta, rut, razon_social, codigo_producto, precio, fecha_actualizacion)
                VALUES(@p, @r, @rs, @c, @pr, @f);
                """;
            cmd.Parameters.AddWithValue("@p", (int)pr.Planta);
            cmd.Parameters.AddWithValue("@r", pr.Rut);
            cmd.Parameters.AddWithValue("@rs", pr.RazonSocial);
            cmd.Parameters.AddWithValue("@c", pr.CodigoProducto);
            cmd.Parameters.AddWithValue("@pr", pr.Precio);
            cmd.Parameters.AddWithValue("@f", pr.FechaActualizacion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using (SqliteCommand idxCmd = conn.CreateCommand())
        {
            idxCmd.Transaction = tx;
            idxCmd.CommandText = """
                INSERT OR IGNORE INTO index_precios(planta, folio, precio, familia, actualizado_en)
                VALUES (1, 10420, 350, 'ALTA', @dt), (1, 10421, 420, 'RECAM', @dt), (2, 20550, 300, 'MIX SUPERMERCADO', @dt);
                INSERT OR IGNORE INTO ultimo_folio_jcramm(id, folio, fecha) VALUES (1, 10400, @f);
                INSERT INTO audit_logs(timestamp_utc, usuario, accion, entidad, registro_id, detalle)
                VALUES (@dt, 'SISTEMA', 'INICIALIZACION', 'BASE_DATOS', '1', 'Base SQLite inicializada con catálogo y registros demostrativos.');
                """;
            idxCmd.Parameters.AddWithValue("@dt", DateTime.UtcNow.ToString("O"));
            idxCmd.Parameters.AddWithValue("@f", today.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            await idxCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
        await using SqliteCommand cmd = conn.CreateCommand();

        List<string> filters = [];
        if (planta.HasValue)
        {
            filters.Add("planta = @planta");
            cmd.Parameters.AddWithValue("@planta", (int)planta.Value);
        }
        if (desde.HasValue)
        {
            filters.Add("fecha_ingreso >= @desde");
            cmd.Parameters.AddWithValue("@desde", desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (hasta.HasValue)
        {
            filters.Add("fecha_ingreso <= @hasta");
            cmd.Parameters.AddWithValue("@hasta", hasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        string where = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty;
        cmd.CommandText = $"""
            SELECT id, folio, planta, tipo_pesaje, patente, peso_bruto, peso_tara, peso_neto,
                   cod_producto, nom_producto, rut_cliente, nom_cliente, origen, destino, guia_despacho,
                   fecha_ingreso, hora_ingreso, fecha_salida, hora_salida, observacion, grupo_material,
                   es_traslado, ciudad_cliente
            FROM pesajes
            {where}
            ORDER BY fecha_ingreso DESC, folio DESC;
            """;

        List<Pesaje> list = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new Pesaje
            {
                Id = rd.GetInt64(0),
                Folio = rd.GetInt64(1),
                Planta = (Planta)rd.GetInt32(2),
                TipoPesaje = (TipoPesaje)rd.GetInt32(3),
                Patente = rd.GetString(4),
                PesoBruto = rd.GetInt32(5),
                PesoTara = rd.GetInt32(6),
                PesoNeto = rd.GetInt32(7),
                CodProducto = rd.GetString(8),
                NomProducto = rd.GetString(9),
                RutCliente = rd.GetString(10),
                NomCliente = rd.GetString(11),
                Origen = rd.GetString(12),
                Destino = rd.GetString(13),
                GuiaDespacho = rd.GetString(14),
                FechaIngreso = ParseDate(rd.GetString(15)),
                HoraIngreso = ParseTime(rd.GetString(16)),
                FechaSalida = ParseDate(rd.GetString(17)),
                HoraSalida = ParseTime(rd.GetString(18)),
                Observacion = rd.GetString(19),
                GrupoMaterial = rd.GetString(20),
                EsTraslado = rd.GetInt32(21) == 1,
                CiudadCliente = rd.IsDBNull(22) ? null : rd.GetString(22)
            });
        }
        return list;
    }

    public async Task<IReadOnlyList<Cliente>> GetClientesAsync(Planta? planta = null, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        if (planta.HasValue)
        {
            cmd.CommandText = "SELECT rut, digito, nombre, razon_social, ciudad, direccion, planta_asociada FROM clientes WHERE planta_asociada = @p OR planta_asociada = 0 ORDER BY razon_social;";
            cmd.Parameters.AddWithValue("@p", (int)planta.Value);
        }
        else
        {
            cmd.CommandText = "SELECT rut, digito, nombre, razon_social, ciudad, direccion, planta_asociada FROM clientes ORDER BY razon_social;";
        }

        List<Cliente> result = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            int pVal = rd.GetInt32(6);
            result.Add(new Cliente
            {
                Rut = rd.GetString(0),
                Digito = rd.GetString(1),
                Nombre = rd.GetString(2),
                RazonSocial = rd.GetString(3),
                Ciudad = rd.GetString(4),
                Direccion = rd.GetString(5),
                PlantaAsociada = pVal == 0 ? null : (Planta)pVal
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<Producto>> GetProductosAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT codigo, descripcion, grupo, unidad_medida FROM productos ORDER BY codigo;";
        List<Producto> result = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new Producto
            {
                Codigo = rd.GetString(0),
                Descripcion = rd.GetString(1),
                Grupo = rd.GetString(2),
                UnidadMedida = rd.GetString(3)
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<PrecioProveedor>> GetPreciosProveedorAsync(Planta? planta = null, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        if (planta.HasValue)
        {
            cmd.CommandText = "SELECT indice, planta, rut, razon_social, codigo_producto, precio, fecha_actualizacion FROM precios_proveedor WHERE planta = @p ORDER BY indice;";
            cmd.Parameters.AddWithValue("@p", (int)planta.Value);
        }
        else
        {
            cmd.CommandText = "SELECT indice, planta, rut, razon_social, codigo_producto, precio, fecha_actualizacion FROM precios_proveedor ORDER BY indice;";
        }

        List<PrecioProveedor> result = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new PrecioProveedor
            {
                Indice = rd.GetInt64(0),
                Planta = (Planta)rd.GetInt32(1),
                Rut = rd.GetString(2),
                RazonSocial = rd.GetString(3),
                CodigoProducto = rd.GetString(4),
                Precio = rd.GetDecimal(5),
                FechaActualizacion = ParseDate(rd.GetString(6))
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<IndexPrecioPesaje>> GetIndexPreciosAsync(Planta? planta = null, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        if (planta.HasValue)
        {
            cmd.CommandText = "SELECT planta, folio, precio, familia, actualizado_en FROM index_precios WHERE planta = @p ORDER BY folio;";
            cmd.Parameters.AddWithValue("@p", (int)planta.Value);
        }
        else
        {
            cmd.CommandText = "SELECT planta, folio, precio, familia, actualizado_en FROM index_precios ORDER BY folio;";
        }

        List<IndexPrecioPesaje> result = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new IndexPrecioPesaje
            {
                Planta = (Planta)rd.GetInt32(0),
                Folio = rd.GetInt64(1),
                Precio = rd.GetDecimal(2),
                Familia = rd.GetString(3),
                ActualizadoEn = DateTime.TryParse(rd.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt)
                    ? dt
                    : DateTime.UtcNow
            });
        }
        return result;
    }

    public async Task<UltimoFolioJcramm> GetUltimoFolioJcrammAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT folio, fecha FROM ultimo_folio_jcramm WHERE id = 1;";
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            return new UltimoFolioJcramm
            {
                Folio = rd.GetInt64(0),
                Fecha = ParseDate(rd.GetString(1))
            };
        }

        return new UltimoFolioJcramm { Folio = 0, Fecha = DateOnly.FromDateTime(DateTime.Today) };
    }

    public async Task SetUltimoFolioJcrammAsync(long folio, DateOnly fecha, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO ultimo_folio_jcramm(id, folio, fecha) VALUES(1, @f, @dt)
            ON CONFLICT(id) DO UPDATE SET folio = excluded.folio, fecha = excluded.fecha;
            """;
        cmd.Parameters.AddWithValue("@f", folio);
        cmd.Parameters.AddWithValue("@dt", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
        {
            Usuario = usuario,
            Accion = "EXPORTACION_EXCEL",
            Entidad = "ultimo_folio_jcramm",
            RegistroId = folio.ToString(CultureInfo.InvariantCulture),
            Detalle = $"Último folio JCRAMM actualizado a #{folio} ({fecha:yyyy-MM-dd})."
        }, ct).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task UpsertPrecioProveedorAsync(PrecioProveedor precio, bool allowUpdate, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = allowUpdate
            ? """
              INSERT INTO precios_proveedor(planta, rut, razon_social, codigo_producto, precio, fecha_actualizacion)
              VALUES(@p, @r, @rs, @c, @pr, @f)
              ON CONFLICT(planta, rut, codigo_producto) DO UPDATE SET
                  razon_social = excluded.razon_social,
                  precio = excluded.precio,
                  fecha_actualizacion = excluded.fecha_actualizacion;
              """
            : """
              INSERT INTO precios_proveedor(planta, rut, razon_social, codigo_producto, precio, fecha_actualizacion)
              VALUES(@p, @r, @rs, @c, @pr, @f);
              """;
        cmd.Parameters.AddWithValue("@p", (int)precio.Planta);
        cmd.Parameters.AddWithValue("@r", precio.Rut);
        cmd.Parameters.AddWithValue("@rs", precio.RazonSocial);
        cmd.Parameters.AddWithValue("@c", precio.CodigoProducto);
        cmd.Parameters.AddWithValue("@pr", precio.Precio);
        cmd.Parameters.AddWithValue("@f", precio.FechaActualizacion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
        {
            Usuario = usuario,
            Accion = allowUpdate ? "ACTUALIZACION_PRECIO_PROV" : "CREACION_PRECIO_PROV",
            Entidad = $"precios_proveedor_{precio.Planta.ToShortTitle().ToLowerInvariant()}",
            RegistroId = $"{precio.Rut}:{precio.CodigoProducto}",
            Detalle = $"Precio ${precio.Precio:N0} asignado a {precio.RazonSocial} ({precio.Rut}) / {precio.CodigoProducto}."
        }, ct).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeletePrecioProveedorAsync(Planta planta, string rut, string codigoProducto, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM precios_proveedor WHERE planta = @p AND rut = @r AND codigo_producto = @c;";
        cmd.Parameters.AddWithValue("@p", (int)planta);
        cmd.Parameters.AddWithValue("@r", rut.Trim());
        cmd.Parameters.AddWithValue("@c", codigoProducto.Trim().ToUpperInvariant());
        int rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (rows > 0)
        {
            await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
            {
                Usuario = usuario,
                Accion = "ELIMINACION_PRECIO_PROV",
                Entidad = $"precios_proveedor_{planta.ToShortTitle().ToLowerInvariant()}",
                RegistroId = $"{rut}:{codigoProducto}",
                Detalle = $"Eliminada tarifa pactada para RUT {rut} y producto {codigoProducto} en {planta}."
            }, ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task UpsertIndexPrecioAsync(IndexPrecioPesaje indexPrecio, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO index_precios(planta, folio, precio, familia, actualizado_en)
            VALUES(@p, @f, @pr, @fam, @dt)
            ON CONFLICT(planta, folio) DO UPDATE SET
                precio = excluded.precio,
                familia = excluded.familia,
                actualizado_en = excluded.actualizado_en;
            """;
        cmd.Parameters.AddWithValue("@p", (int)indexPrecio.Planta);
        cmd.Parameters.AddWithValue("@f", indexPrecio.Folio);
        cmd.Parameters.AddWithValue("@pr", indexPrecio.Precio);
        cmd.Parameters.AddWithValue("@fam", indexPrecio.Familia);
        cmd.Parameters.AddWithValue("@dt", indexPrecio.ActualizadoEn.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
        {
            Usuario = usuario,
            Accion = "ACTUALIZACION_PRECIO_REAL",
            Entidad = $"index_pesaje_{indexPrecio.Planta.ToShortTitle().ToLowerInvariant()}",
            RegistroId = indexPrecio.Folio.ToString(CultureInfo.InvariantCulture),
            Detalle = $"Actualizado precio real a ${indexPrecio.Precio:N0} para folio #{indexPrecio.Folio} ({indexPrecio.Planta})."
        }, ct).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> BulkFreezeIndexPreciosAsync(IEnumerable<IndexPrecioPesaje> items, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        int count = 0;
        foreach (IndexPrecioPesaje item in items)
        {
            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO index_precios(planta, folio, precio, familia, actualizado_en)
                VALUES(@p, @f, @pr, @fam, @dt);
                """;
            cmd.Parameters.AddWithValue("@p", (int)item.Planta);
            cmd.Parameters.AddWithValue("@f", item.Folio);
            cmd.Parameters.AddWithValue("@pr", item.Precio);
            cmd.Parameters.AddWithValue("@fam", item.Familia);
            cmd.Parameters.AddWithValue("@dt", item.ActualizadoEn.ToString("O"));
            count += await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (count > 0)
        {
            await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
            {
                Usuario = usuario,
                Accion = "CONGELAMIENTO_PRECIOS",
                Entidad = "index_precios",
                Detalle = $"Congelados {count} precios teóricos como precios reales indexados."
            }, ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return count;
    }

    public async Task<(int Imported, int Duplicates)> ImportPesajesAsync(
        IEnumerable<Pesaje> pesajes,
        Planta planta,
        string usuario,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

        List<Pesaje> existing = [];
        await using (SqliteCommand exCmd = conn.CreateCommand())
        {
            exCmd.CommandText = "SELECT folio, hora_ingreso FROM pesajes WHERE planta = @p;";
            exCmd.Parameters.AddWithValue("@p", (int)planta);
            await using SqliteDataReader rd = await exCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                existing.Add(new Pesaje
                {
                    Folio = rd.GetInt64(0),
                    Planta = planta,
                    HoraIngreso = ParseTime(rd.GetString(1))
                });
            }
        }

        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        int imported = 0;
        int duplicates = 0;

        foreach (Pesaje p in pesajes)
        {
            if (BusinessRules.IsDuplicatePesaje(existing, p.Folio, planta, p.HoraIngreso))
            {
                duplicates++;
                continue;
            }

            await InsertPesajeInternalAsync(conn, tx, p, ct).ConfigureAwait(false);
            existing.Add(p);
            imported++;
        }

        if (imported > 0)
        {
            await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
            {
                Usuario = usuario,
                Accion = "IMPORTACION_PESAJES",
                Entidad = $"PESAJE_{planta.ToDisplayName()}",
                Detalle = $"Importados {imported} pesajes en {planta} ({duplicates} duplicados omitidos)."
            }, ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return (imported, duplicates);
    }

    public async Task UpsertClientesAsync(IEnumerable<Cliente> clientes, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

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
                INSERT INTO clientes(rut, digito, nombre, razon_social, ciudad, direccion, planta_asociada)
                VALUES(@r, @d, @n, @rs, @ci, @di, @p)
                ON CONFLICT(rut, planta_asociada) DO UPDATE SET
                    digito = excluded.digito,
                    nombre = excluded.nombre,
                    razon_social = excluded.razon_social,
                    ciudad = excluded.ciudad,
                    direccion = excluded.direccion;
                """;
            cmd.Parameters.AddWithValue("@r", c.Rut.Trim());
            cmd.Parameters.AddWithValue("@d", c.Digito.Trim());
            cmd.Parameters.AddWithValue("@n", c.Nombre.Trim());
            cmd.Parameters.AddWithValue("@rs", c.RazonSocial.Trim());
            cmd.Parameters.AddWithValue("@ci", c.Ciudad.Trim());
            cmd.Parameters.AddWithValue("@di", c.Direccion.Trim());
            cmd.Parameters.AddWithValue("@p", c.PlantaAsociada.HasValue ? (int)c.PlantaAsociada.Value : 0);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            count++;
        }

        if (count > 0)
        {
            await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
            {
                Usuario = usuario,
                Accion = "SINCRONIZACION_CLIENTES",
                Entidad = "clientes",
                Detalle = $"Sincronizados {count} clientes/proveedores."
            }, ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task UpsertProductosAsync(IEnumerable<Producto> productos, string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

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
                    grupo = excluded.grupo,
                    unidad_medida = excluded.unidad_medida;
                """;
            cmd.Parameters.AddWithValue("@c", p.Codigo.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@d", p.Descripcion.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@g", p.Grupo.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@u", p.UnidadMedida.Trim().ToUpperInvariant());
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            count++;
        }

        if (count > 0)
        {
            await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
            {
                Usuario = usuario,
                Accion = "SINCRONIZACION_PRODUCTOS",
                Entidad = "productos",
                Detalle = $"Sincronizados {count} materiales de catálogo."
            }, ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> PurgeAllPesajesAsync(string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using SqliteCommand delCmd = conn.CreateCommand();
        delCmd.Transaction = tx;
        delCmd.CommandText = "DELETE FROM pesajes;";
        int deleted = await delCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
        {
            Usuario = usuario,
            Accion = "PURGA_TABLAS",
            Entidad = "pesajes",
            Detalle = $"Purga crítica completada: {deleted} registros eliminados."
        }, ct).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return deleted;
    }

    public async Task ResetDemoDataAsync(string usuario, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            await using SqliteCommand del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = "DELETE FROM pesajes; DELETE FROM precios_proveedor; DELETE FROM index_precios; DELETE FROM ultimo_folio_jcramm;";
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }

        await InsertSeedDemoOperationalDataAsync(conn, ct).ConfigureAwait(false);
        await AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = usuario,
            Accion = "RESTABLECER_DEMO",
            Entidad = "SISTEMA",
            Detalle = "Datos demostrativos restablecidos."
        }, ct).ConfigureAwait(false);
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
            filters.Add("usuario LIKE @u");
            cmd.Parameters.AddWithValue("@u", $"%{usuario.Trim()}%");
        }

        string where = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty;
        cmd.CommandText = $"SELECT id, timestamp_utc, usuario, accion, entidad, registro_id, detalle FROM audit_logs {where} ORDER BY id DESC LIMIT @lim;";
        cmd.Parameters.AddWithValue("@lim", Math.Clamp(limit, 1, 5000));

        List<AuditLogEntry> logs = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            logs.Add(new AuditLogEntry
            {
                Id = rd.GetInt64(0),
                TimestampUtc = DateTime.TryParse(rd.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime ts) ? ts : DateTime.UtcNow,
                Usuario = rd.GetString(2),
                Accion = rd.GetString(3),
                Entidad = rd.GetString(4),
                RegistroId = rd.GetString(5),
                Detalle = rd.GetString(6)
            });
        }
        return logs;
    }

    public async Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await InsertAuditInternalAsync(conn, tx, entry, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Usuario>> GetUsuariosAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nombre_usuario, nombre_completo, rol, password_hash, password_salt, activo, intentos_fallidos, bloqueado_hasta_utc, permisos_json, creado_en_utc FROM usuarios ORDER BY id;";

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
        cmd.CommandText = "SELECT id, nombre_usuario, nombre_completo, rol, password_hash, password_salt, activo, intentos_fallidos, bloqueado_hasta_utc, permisos_json, creado_en_utc FROM usuarios WHERE nombre_usuario = @u LIMIT 1;";
        cmd.Parameters.AddWithValue("@u", nombreUsuario.Trim());

        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false) ? MapUsuario(rd) : null;
    }

    public async Task SaveUsuarioAsync(Usuario usuario, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        string permsJson = JsonSerializer.Serialize(usuario.PermisosPersonalizados);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        if (usuario.Id > 0)
        {
            cmd.CommandText = """
                UPDATE usuarios
                SET nombre_usuario = @u,
                    nombre_completo = @n,
                    rol = @r,
                    password_hash = @h,
                    password_salt = @s,
                    activo = @a,
                    intentos_fallidos = @ifal,
                    bloqueado_hasta_utc = @bloq,
                    permisos_json = @pj
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", usuario.Id);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO usuarios(nombre_usuario, nombre_completo, rol, password_hash, password_salt, activo, intentos_fallidos, bloqueado_hasta_utc, permisos_json, creado_en_utc)
                VALUES(@u, @n, @r, @h, @s, @a, @ifal, @bloq, @pj, @c);
                """;
            cmd.Parameters.AddWithValue("@c", usuario.CreadoEnUtc.ToString("O"));
        }

        cmd.Parameters.AddWithValue("@u", usuario.NombreUsuario);
        cmd.Parameters.AddWithValue("@n", usuario.NombreCompleto);
        cmd.Parameters.AddWithValue("@r", (int)usuario.Rol);
        cmd.Parameters.AddWithValue("@h", usuario.PasswordHash);
        cmd.Parameters.AddWithValue("@s", usuario.PasswordSalt);
        cmd.Parameters.AddWithValue("@a", usuario.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@ifal", usuario.IntentosFallidos);
        cmd.Parameters.AddWithValue("@bloq", usuario.BloqueadoHastaUtc.HasValue ? usuario.BloqueadoHastaUtc.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@pj", permsJson);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (!string.Equals(actor, "SISTEMA", StringComparison.Ordinal))
        {
            await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
            {
                Usuario = actor,
                Accion = usuario.Id > 0 ? "ACTUALIZACION_USUARIO" : "CREACION_USUARIO",
                Entidad = "usuarios",
                RegistroId = usuario.NombreUsuario,
                Detalle = $"Guardado perfil '{usuario.NombreUsuario}' con rol {usuario.Rol} (Activo={usuario.Activo})."
            }, ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteUsuarioAsync(long id, string actor, CancellationToken ct = default)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using SqliteConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM usuarios WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await InsertAuditInternalAsync(conn, tx, new AuditLogEntry
        {
            Usuario = actor,
            Accion = "ELIMINACION_USUARIO",
            Entidad = "usuarios",
            RegistroId = id.ToString(CultureInfo.InvariantCulture),
            Detalle = $"Usuario ID {id} eliminado."
        }, ct).ConfigureAwait(false);

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

        await using SqliteConnection source = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteConnection dest = new($"Data Source={destinationPath}");
        await dest.OpenAsync(ct).ConfigureAwait(false);
        source.BackupDatabase(dest);
        return destinationPath;
    }

    private static async Task InsertPesajeInternalAsync(SqliteConnection conn, SqliteTransaction tx, Pesaje p, CancellationToken ct)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO pesajes(
                folio, planta, tipo_pesaje, patente, peso_bruto, peso_tara, peso_neto,
                cod_producto, nom_producto, rut_cliente, nom_cliente, origen, destino, guia_despacho,
                fecha_ingreso, hora_ingreso, fecha_salida, hora_salida, observacion, grupo_material,
                es_traslado, ciudad_cliente)
            VALUES(
                @folio, @planta, @tipo, @pat, @bruto, @tara, @neto,
                @codp, @nomp, @rut, @nomc, @ori, @dest, @guia,
                @fecing, @horing, @fecsal, @horsal, @obs, @grp,
                @tras, @ciud);
            """;
        cmd.Parameters.AddWithValue("@folio", p.Folio);
        cmd.Parameters.AddWithValue("@planta", (int)p.Planta);
        cmd.Parameters.AddWithValue("@tipo", (int)p.TipoPesaje);
        cmd.Parameters.AddWithValue("@pat", p.Patente);
        cmd.Parameters.AddWithValue("@bruto", p.PesoBruto);
        cmd.Parameters.AddWithValue("@tara", p.PesoTara);
        cmd.Parameters.AddWithValue("@neto", p.PesoNeto);
        cmd.Parameters.AddWithValue("@codp", p.CodProducto);
        cmd.Parameters.AddWithValue("@nomp", p.NomProducto);
        cmd.Parameters.AddWithValue("@rut", p.RutCliente);
        cmd.Parameters.AddWithValue("@nomc", p.NomCliente);
        cmd.Parameters.AddWithValue("@ori", p.Origen);
        cmd.Parameters.AddWithValue("@dest", p.Destino);
        cmd.Parameters.AddWithValue("@guia", p.GuiaDespacho);
        cmd.Parameters.AddWithValue("@fecing", p.FechaIngreso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@horing", p.HoraIngreso.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@fecsal", p.FechaSalida.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@horsal", p.HoraSalida.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@obs", p.Observacion);
        cmd.Parameters.AddWithValue("@grp", p.GrupoMaterial);
        cmd.Parameters.AddWithValue("@tras", p.EsTraslado ? 1 : 0);
        cmd.Parameters.AddWithValue("@ciud", string.IsNullOrWhiteSpace(p.CiudadCliente) ? DBNull.Value : p.CiudadCliente);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task InsertAuditInternalAsync(SqliteConnection conn, SqliteTransaction tx, AuditLogEntry entry, CancellationToken ct)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO audit_logs(timestamp_utc, usuario, accion, entidad, registro_id, detalle)
            VALUES(@ts, @u, @ac, @ent, @rid, @det);
            """;
        cmd.Parameters.AddWithValue("@ts", entry.TimestampUtc.ToString("O"));
        cmd.Parameters.AddWithValue("@u", entry.Usuario);
        cmd.Parameters.AddWithValue("@ac", entry.Accion);
        cmd.Parameters.AddWithValue("@ent", entry.Entidad);
        cmd.Parameters.AddWithValue("@rid", entry.RegistroId);
        cmd.Parameters.AddWithValue("@det", entry.Detalle);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static Usuario MapUsuario(SqliteDataReader rd)
    {
        PermisoSistema[] perms = [];
        try
        {
            string json = rd.GetString(9);
            if (!string.IsNullOrWhiteSpace(json))
            {
                perms = JsonSerializer.Deserialize<PermisoSistema[]>(json) ?? [];
            }
        }
        catch
        {
            perms = [];
        }

        DateTime? bloq = null;
        if (!rd.IsDBNull(8) && DateTime.TryParse(rd.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime bdt))
        {
            bloq = bdt;
        }

        DateTime creado = DateTime.TryParse(rd.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime cdt)
            ? cdt
            : DateTime.UtcNow;

        return new Usuario
        {
            Id = rd.GetInt64(0),
            NombreUsuario = rd.GetString(1),
            NombreCompleto = rd.GetString(2),
            Rol = (RolUsuario)rd.GetInt32(3),
            PasswordHash = rd.GetString(4),
            PasswordSalt = rd.GetString(5),
            Activo = rd.GetInt32(6) == 1,
            IntentosFallidos = rd.GetInt32(7),
            BloqueadoHastaUtc = bloq,
            PermisosPersonalizados = perms,
            CreadoEnUtc = creado
        };
    }

    private static DateOnly ParseDate(string raw) =>
        DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out DateOnly d)
            ? d
            : DateOnly.FromDateTime(DateTime.Today);

    private static TimeOnly ParseTime(string raw) =>
        TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out TimeOnly t)
            ? t
            : TimeOnly.MinValue;

    public void Dispose()
    {
        _initLock.Dispose();
    }
}
