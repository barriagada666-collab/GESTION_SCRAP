using System.Globalization;
using System.Security.Cryptography;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Application.Services;

public static class RolePermissionMatrix
{
    private static readonly Dictionary<RolUsuario, PermisoSistema[]> Matrix = new()
        {
            [RolUsuario.Administrador] = Enum.GetValues<PermisoSistema>(),
            [RolUsuario.Supervisor] =
            [
                PermisoSistema.VerDashboard,
                PermisoSistema.ConsultarInformes,
                PermisoSistema.ExportarExcel,
                PermisoSistema.EditarPreciosReales,
                PermisoSistema.CongelarPreciosTeoricos,
                PermisoSistema.AdministrarMantenedorPrecios,
                PermisoSistema.CerrarFolioJcramm,
                PermisoSistema.ImportarFirebird,
                PermisoSistema.EjecutarProcedimientosFirebird,
                PermisoSistema.VerAuditoria
            ],
            [RolUsuario.Operador] =
            [
                PermisoSistema.VerDashboard,
                PermisoSistema.ConsultarInformes,
                PermisoSistema.ExportarExcel,
                PermisoSistema.ImportarFirebird
            ],
            [RolUsuario.Auditor] =
            [
                PermisoSistema.VerDashboard,
                PermisoSistema.ConsultarInformes,
                PermisoSistema.ExportarExcel,
                PermisoSistema.VerAuditoria
            ]
        };

    public static IReadOnlyCollection<PermisoSistema> GetDefaultsForRole(RolUsuario rol) =>
        Matrix.TryGetValue(rol, out PermisoSistema[]? perms)
            ? perms
            : [PermisoSistema.VerDashboard, PermisoSistema.ConsultarInformes];
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
                Rol = user.Rol,
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
                : "Credenciales inválidas.", null);
        }

        if (user.IntentosFallidos > 0 || user.BloqueadoHastaUtc.HasValue)
        {
            user = new Usuario
            {
                Id = user.Id,
                NombreUsuario = user.NombreUsuario,
                NombreCompleto = user.NombreCompleto,
                Rol = user.Rol,
                PasswordHash = user.PasswordHash,
                PasswordSalt = user.PasswordSalt,
                Activo = user.Activo,
                IntentosFallidos = 0,
                BloqueadoHastaUtc = null,
                PermisosPersonalizados = user.PermisosPersonalizados,
                CreadoEnUtc = user.CreadoEnUtc
            };
            await _repository.SaveUsuarioAsync(user, user.NombreUsuario, ct).ConfigureAwait(false);
        }

        CurrentUser = user;
        await _repository.AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = user.NombreUsuario,
            Accion = "LOGIN_EXITOSO",
            Entidad = "SEGURIDAD",
            RegistroId = user.Id.ToString(CultureInfo.InvariantCulture),
            Detalle = $"Inicio de sesión con perfil {user.Rol}."
        }, ct).ConfigureAwait(false);

        return (true, $"Bienvenido, {user.NombreCompleto}.", user);
    }

    public void Logout()
    {
        CurrentUser = null;
    }

    public IReadOnlyCollection<PermisoSistema> GetEffectivePermissions(Usuario usuario)
    {
        if (usuario.PermisosPersonalizados.Count > 0)
        {
            return usuario.PermisosPersonalizados;
        }

        return RolePermissionMatrix.GetDefaultsForRole(usuario.Rol);
    }

    public bool HasPermission(PermisoSistema permiso)
    {
        if (CurrentUser is null || !CurrentUser.Activo)
        {
            return false;
        }

        return GetEffectivePermissions(CurrentUser).Contains(permiso);
    }

    public void EnsurePermission(PermisoSistema permiso)
    {
        if (CurrentUser is null)
        {
            throw new UnauthorizedAccessException("Debe iniciar sesión para realizar esta acción.");
        }

        if (!HasPermission(permiso))
        {
            throw new UnauthorizedAccessException(
                $"El perfil '{CurrentUser.Rol}' no posee el permiso requerido ({permiso}).");
        }
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (CurrentUser is null)
        {
            throw new UnauthorizedAccessException("No hay sesión activa.");
        }

        if (!PasswordSecurity.VerifyPassword(currentPassword, CurrentUser.PasswordHash, CurrentUser.PasswordSalt))
        {
            return false;
        }

        (bool valid, string message) = PasswordSecurity.ValidateComplexity(newPassword);
        if (!valid)
        {
            throw new ArgumentException(message, nameof(newPassword));
        }

        (string hash, string salt) = PasswordSecurity.HashPassword(newPassword);
        Usuario updated = new()
        {
            Id = CurrentUser.Id,
            NombreUsuario = CurrentUser.NombreUsuario,
            NombreCompleto = CurrentUser.NombreCompleto,
            Rol = CurrentUser.Rol,
            PasswordHash = hash,
            PasswordSalt = salt,
            Activo = CurrentUser.Activo,
            IntentosFallidos = 0,
            BloqueadoHastaUtc = null,
            PermisosPersonalizados = CurrentUser.PermisosPersonalizados,
            CreadoEnUtc = CurrentUser.CreadoEnUtc
        };

        await _repository.SaveUsuarioAsync(updated, CurrentUser.NombreUsuario, ct).ConfigureAwait(false);
        await _repository.AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = CurrentUser.NombreUsuario,
            Accion = "CAMBIO_CLAVE",
            Entidad = "SEGURIDAD",
            RegistroId = CurrentUser.Id.ToString(CultureInfo.InvariantCulture),
            Detalle = "Contraseña actualizada por el usuario."
        }, ct).ConfigureAwait(false);

        CurrentUser = updated;
        return true;
    }
}
