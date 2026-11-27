using System.Globalization;
using System.Security.Cryptography;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Application.Services;

public static class DefaultRoleProfiles
{
    public static IReadOnlyList<PerfilUsuario> GetSeededProfiles() =>
    [
        new PerfilUsuario
        {
            Id = 1,
            Nombre = "Administrador",
            Descripcion = "Acceso completo a CRM, ERP, pesajes, inventario, informes, grupos, importador, perfiles y auditoría.",
            EsSistema = true,
            Permisos = Enum.GetValues<PermisoSistema>()
        },
        new PerfilUsuario
        {
            Id = 2,
            Nombre = "Supervisor",
            Descripcion = "Acceso operativo completo, CRM, ERP, autorización/anulación de pesajes, inventario y auditoría.",
            EsSistema = false,
            Permisos =
            [
                PermisoSistema.VerInformeResumen,
                PermisoSistema.VerGrupoMaterial,
                PermisoSistema.AdministrarGrupoMaterial,
                PermisoSistema.VerAnalisisProveedores,
                PermisoSistema.ImportarDatos,
                PermisoSistema.ExportarExcel,
                PermisoSistema.VerAuditoria,
                PermisoSistema.AdministrarCrmEntidades,
                PermisoSistema.AdministrarMaterialesVehiculos,
                PermisoSistema.GestionarPesajesOperaciones,
                PermisoSistema.AutorizarAnularPesajesInventario
            ]
        },
        new PerfilUsuario
        {
            Id = 3,
            Nombre = "Analista",
            Descripcion = "Gestión de grupos de material, informes empresariales, análisis de proveedores y Excel.",
            EsSistema = false,
            Permisos =
            [
                PermisoSistema.VerInformeResumen,
                PermisoSistema.VerGrupoMaterial,
                PermisoSistema.AdministrarGrupoMaterial,
                PermisoSistema.VerAnalisisProveedores,
                PermisoSistema.ExportarExcel
            ]
        },
        new PerfilUsuario
        {
            Id = 4,
            Nombre = "Operador",
            Descripcion = "Registro de pesajes (1 y 2 pasos), órdenes ERP, importación Firebird/Access y consultas.",
            EsSistema = false,
            Permisos =
            [
                PermisoSistema.VerInformeResumen,
                PermisoSistema.VerGrupoMaterial,
                PermisoSistema.VerAnalisisProveedores,
                PermisoSistema.ImportarDatos,
                PermisoSistema.ExportarExcel,
                PermisoSistema.GestionarPesajesOperaciones
            ]
        },
        new PerfilUsuario
        {
            Id = 5,
            Nombre = "Auditor",
            Descripcion = "Acceso de solo lectura a informes, exportación Excel y bitácora de auditoría.",
            EsSistema = false,
            Permisos =
            [
                PermisoSistema.VerInformeResumen,
                PermisoSistema.VerGrupoMaterial,
                PermisoSistema.VerAnalisisProveedores,
                PermisoSistema.ExportarExcel,
                PermisoSistema.VerAuditoria
            ]
        }
    ];
}

public static class PasswordSecurity
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static (string HashBase64, string SaltBase64) HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string expectedHashBase64, string saltBase64)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(expectedHashBase64) ||
            string.IsNullOrWhiteSpace(saltBase64))
        {
            return false;
        }

        byte[] salt = Convert.FromBase64String(saltBase64);
        byte[] expected = Convert.FromBase64String(expectedHashBase64);
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static (bool Valid, string Message) ValidateComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return (false, "La contraseña debe tener al menos 8 caracteres.");
        }

        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);

        if (!hasUpper || !hasLower || !hasDigit)
        {
            return (false, "La contraseña debe incluir mayúsculas, minúsculas y al menos un dígito.");
        }

        return (true, "Contraseña válida.");
    }
}

public sealed class SecuritySessionService(IScrapRepository repository) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly IScrapRepository _repository = repository;
    private PerfilUsuario? _currentPerfil;

    public Usuario? CurrentUser { get; private set; }

    public async Task<(bool Success, string Message, Usuario? User)> LoginAsync(
        string username,
        string password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Ingrese usuario y contraseña.", null);
        }

        string normalizedUser = username.Trim();
        Usuario? user = await _repository.GetUsuarioByNombreAsync(normalizedUser, ct).ConfigureAwait(false);
        if (user is null)
        {
            await _repository.AddAuditLogAsync(new AuditLogEntry
            {
                Usuario = normalizedUser,
                Accion = "LOGIN_FALLIDO",
                Entidad = "SEGURIDAD",
                Detalle = "Intento de inicio de sesión con usuario inexistente."
            }, ct).ConfigureAwait(false);
            return (false, "Credenciales inválidas.", null);
        }

        if (!user.Activo)
        {
            return (false, "La cuenta de usuario se encuentra desactivada. Contacte al administrador.", null);
        }

        if (user.BloqueadoHastaUtc.HasValue && user.BloqueadoHastaUtc.Value > DateTime.UtcNow)
        {
            int mins = Math.Max(1, (int)Math.Ceiling((user.BloqueadoHastaUtc.Value - DateTime.UtcNow).TotalMinutes));
            return (false, $"Cuenta bloqueada temporalmente por intentos fallidos. Intente en {mins} minuto(s).", null);
        }

        bool valid = PasswordSecurity.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
        if (!valid)
        {
            int nextAttempts = user.IntentosFallidos + 1;
            DateTime? lockUntil = nextAttempts >= MaxFailedAttempts ? DateTime.UtcNow.Add(LockoutDuration) : null;

            Usuario updated = new()
            {
                Id = user.Id,
                NombreUsuario = user.NombreUsuario,
                NombreCompleto = user.NombreCompleto,
                PerfilId = user.PerfilId,
                NombrePerfil = user.NombrePerfil,
                PasswordHash = user.PasswordHash,
                PasswordSalt = user.PasswordSalt,
                Activo = user.Activo,
                IntentosFallidos = lockUntil.HasValue ? 0 : nextAttempts,
                BloqueadoHastaUtc = lockUntil,
                PermisosPersonalizados = user.PermisosPersonalizados,
                CreadoEnUtc = user.CreadoEnUtc
            };
            await _repository.SaveUsuarioAsync(updated, "SISTEMA", ct).ConfigureAwait(false);
            await _repository.AddAuditLogAsync(new AuditLogEntry
            {
                Usuario = user.NombreUsuario,
                Accion = "LOGIN_FALLIDO",
                Entidad = "SEGURIDAD",
                RegistroId = user.Id.ToString(CultureInfo.InvariantCulture),
                Detalle = lockUntil.HasValue
                    ? "Cuenta bloqueada temporalmente tras 5 intentos fallidos."
                    : $"Contraseña incorrecta (intento {nextAttempts}/{MaxFailedAttempts})."
            }, ct).ConfigureAwait(false);

            return (false, lockUntil.HasValue
                ? "Demasiados intentos fallidos. Cuenta bloqueada por 5 minutos."
                : $"Credenciales inválidas (intento {nextAttempts} de {MaxFailedAttempts}).", null);
        }

        if (user.IntentosFallidos > 0 || user.BloqueadoHastaUtc.HasValue)
        {
            user = new Usuario
            {
                Id = user.Id,
                NombreUsuario = user.NombreUsuario,
                NombreCompleto = user.NombreCompleto,
                PerfilId = user.PerfilId,
                NombrePerfil = user.NombrePerfil,
                PasswordHash = user.PasswordHash,
                PasswordSalt = user.PasswordSalt,
                Activo = user.Activo,
                IntentosFallidos = 0,
                BloqueadoHastaUtc = null,
                PermisosPersonalizados = user.PermisosPersonalizados,
                CreadoEnUtc = user.CreadoEnUtc
            };
            await _repository.SaveUsuarioAsync(user, "SISTEMA", ct).ConfigureAwait(false);
        }

        _currentPerfil = await _repository.GetPerfilByIdAsync(user.PerfilId, ct).ConfigureAwait(false);
        CurrentUser = user;

        await _repository.AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = user.NombreUsuario,
            Accion = "LOGIN_EXITOSO",
            Entidad = "SEGURIDAD",
            RegistroId = user.Id.ToString(CultureInfo.InvariantCulture),
            Detalle = $"Inicio de sesión exitoso (Perfil: {user.NombrePerfil})."
        }, ct).ConfigureAwait(false);

        return (true, "Autenticación exitosa.", user);
    }

    public void Logout()
    {
        CurrentUser = null;
        _currentPerfil = null;
    }

    public bool HasPermission(PermisoSistema permiso)
    {
        if (CurrentUser is null)
        {
            return false;
        }

        return GetEffectivePermissions(CurrentUser, _currentPerfil).Contains(permiso);
    }

    public void EnsurePermission(PermisoSistema permiso)
    {
        if (CurrentUser is null)
        {
            throw new UnauthorizedAccessException("Debe iniciar sesión para ejecutar esta operación.");
        }

        if (!HasPermission(permiso))
        {
            throw new UnauthorizedAccessException($"El perfil '{CurrentUser.NombrePerfil}' no cuenta con el permiso requerido ({permiso}).");
        }
    }

    public IReadOnlyCollection<PermisoSistema> GetEffectivePermissions(Usuario usuario, PerfilUsuario? perfil = null)
    {
        if (usuario.PermisosPersonalizados.Count > 0)
        {
            return usuario.PermisosPersonalizados;
        }

        if (perfil is not null && perfil.Id == usuario.PerfilId && perfil.Permisos.Count > 0)
        {
            return perfil.Permisos;
        }

        PerfilUsuario? seeded = DefaultRoleProfiles.GetSeededProfiles()
            .FirstOrDefault(p => p.Id == usuario.PerfilId || string.Equals(p.Nombre, usuario.NombrePerfil, StringComparison.OrdinalIgnoreCase));

        return seeded?.Permisos ?? [PermisoSistema.VerInformeResumen];
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (CurrentUser is null)
        {
            throw new UnauthorizedAccessException("No hay una sesión autenticada.");
        }

        if (!PasswordSecurity.VerifyPassword(currentPassword, CurrentUser.PasswordHash, CurrentUser.PasswordSalt))
        {
            return false;
        }

        (bool valid, string msg) = PasswordSecurity.ValidateComplexity(newPassword);
        if (!valid)
        {
            throw new ArgumentException(msg, nameof(newPassword));
        }

        (string hash, string salt) = PasswordSecurity.HashPassword(newPassword);
        Usuario updated = new()
        {
            Id = CurrentUser.Id,
            NombreUsuario = CurrentUser.NombreUsuario,
            NombreCompleto = CurrentUser.NombreCompleto,
            PerfilId = CurrentUser.PerfilId,
            NombrePerfil = CurrentUser.NombrePerfil,
            PasswordHash = hash,
            PasswordSalt = salt,
            Activo = CurrentUser.Activo,
            IntentosFallidos = 0,
            BloqueadoHastaUtc = null,
            PermisosPersonalizados = CurrentUser.PermisosPersonalizados,
            CreadoEnUtc = CurrentUser.CreadoEnUtc
        };

        await _repository.SaveUsuarioAsync(updated, CurrentUser.NombreUsuario, ct).ConfigureAwait(false);
        CurrentUser = updated;

        await _repository.AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = CurrentUser.NombreUsuario,
            Accion = "CAMBIO_CLAVE",
            Entidad = "SEGURIDAD",
            RegistroId = CurrentUser.Id.ToString(CultureInfo.InvariantCulture),
            Detalle = "El usuario actualizó su contraseña personal."
        }, ct).ConfigureAwait(false);

        return true;
    }
}
