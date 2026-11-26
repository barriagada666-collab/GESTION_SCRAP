using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Tests.Unit;

public sealed class BusinessRulesAndSecurityTests
{
    [Theory]
    [InlineData("76.218.764-5", "76218764-5", true)]
    [InlineData("07629801-K", "7629801k", true)]
    [InlineData("150713757", "15071375", true)]
    [InlineData("0015071375-7", "15071375", true)]
    [InlineData("76218764-5", "96781200-K", false)]
    [InlineData("", "76218764-5", false)]
    public void RutChileno_MatchesVariantsCorrectly(string left, string right, bool expected)
    {
        Assert.Equal(expected, RutChileno.Matches(left, right));
    }

    [Fact]
    public void WeightedAverage_PreventsDivisionByZeroAndRoundsAwayFromZero()
    {
        Assert.Equal(0m, BusinessRules.CalculateWeightedAverage(100_000m, 0));
        Assert.Equal(0m, BusinessRules.CalculateWeightedAverage(0m, 500));
        Assert.Equal(150.56m, BusinessRules.CalculateWeightedAverage(301_115m, 2000));
    }

    [Fact]
    public void ValidateCanUpdatePrice_AllowsOnlyRealPricesMode()
    {
        (bool teoAllowed, _) = BusinessRules.ValidateCanUpdatePrice(ModoPrecio.Teoricos);
        (bool realAllowed, _) = BusinessRules.ValidateCanUpdatePrice(ModoPrecio.Reales);

        Assert.False(teoAllowed);
        Assert.True(realAllowed);
    }

    [Fact]
    public void IsDuplicatePesaje_HandlesTalcahuanoHoraIngresoAndOtherPlantsFolioOnly()
    {
        Pesaje[] existing =
        [
            new() { Planta = Planta.Placilla, Folio = 1001, HoraIngreso = new TimeOnly(8, 30) },
            new() { Planta = Planta.Talcahuano, Folio = 2002, HoraIngreso = new TimeOnly(9, 15) }
        ];

        Assert.True(BusinessRules.IsDuplicatePesaje(existing, 1001, Planta.Placilla, new TimeOnly(14, 0)));
        Assert.False(BusinessRules.IsDuplicatePesaje(existing, 2002, Planta.Talcahuano, new TimeOnly(10, 0)));
        Assert.True(BusinessRules.IsDuplicatePesaje(existing, 2002, Planta.Talcahuano, new TimeOnly(9, 15)));
    }

    [Fact]
    public void SpecialGroups_IsolatePesqueraAndAgrosuperWithoutCollision()
    {
        Pesaje agrosuper = new()
        {
            Planta = Planta.Placilla,
            TipoPesaje = TipoPesaje.RecepcionCarga,
            RutCliente = "78483600",
            NomCliente = "AGROSUPER COMERCIAL LTDA",
            CodProducto = "5301001000"
        };

        Pesaje pesquera = new()
        {
            Planta = Planta.Placilla,
            TipoPesaje = TipoPesaje.RecepcionCarga,
            RutCliente = "76658930",
            NomCliente = "PESQUERA AUSTRAL S.A.",
            CodProducto = "5301002000"
        };

        Assert.True(BusinessRules.MatchesAgrosuper(agrosuper));
        Assert.False(BusinessRules.MatchesPesquera(agrosuper));

        Assert.True(BusinessRules.MatchesPesquera(pesquera));
        Assert.False(BusinessRules.MatchesAgrosuper(pesquera));
    }

    [Fact]
    public void IsJcrammEligible_ExcludesBlacklistedMaterialCodes()
    {
        Pesaje eligible = new() { TipoPesaje = TipoPesaje.RecepcionCarga, CodProducto = "S0400" };
        Pesaje carton = new() { TipoPesaje = TipoPesaje.RecepcionCarga, CodProducto = "CARTON" };
        Pesaje sapExcluded = new() { TipoPesaje = TipoPesaje.RecepcionCarga, CodProducto = "5301001003" };
        Pesaje despacho = new() { TipoPesaje = TipoPesaje.DespachoCarga, CodProducto = "S0400" };

        Assert.True(BusinessRules.IsJcrammEligible(eligible));
        Assert.False(BusinessRules.IsJcrammEligible(carton));
        Assert.False(BusinessRules.IsJcrammEligible(sapExcluded));
        Assert.False(BusinessRules.IsJcrammEligible(despacho));
    }

    [Fact]
    public void PasswordSecurity_HashesWithUniqueSaltsAndValidatesComplexity()
    {
        (string hash1, string salt1) = PasswordSecurity.HashPassword("ClaveSegura2026");
        (string hash2, string salt2) = PasswordSecurity.HashPassword("ClaveSegura2026");

        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordSecurity.VerifyPassword("ClaveSegura2026", hash1, salt1));
        Assert.False(PasswordSecurity.VerifyPassword("Incorrecta99", hash1, salt1));

        Assert.False(PasswordSecurity.ValidateComplexity("abc").Valid);
        Assert.False(PasswordSecurity.ValidateComplexity("minusculas1").Valid);
        Assert.True(PasswordSecurity.ValidateComplexity("Valida2026").Valid);
    }

    [Fact]
    public void RolePermissionMatrix_EnforcesLeastPrivilegePerRole()
    {
        IReadOnlyCollection<PermisoSistema> admin = RolePermissionMatrix.GetDefaultsForRole(RolUsuario.Administrador);
        IReadOnlyCollection<PermisoSistema> supervisor = RolePermissionMatrix.GetDefaultsForRole(RolUsuario.Supervisor);
        IReadOnlyCollection<PermisoSistema> operador = RolePermissionMatrix.GetDefaultsForRole(RolUsuario.Operador);
        IReadOnlyCollection<PermisoSistema> auditor = RolePermissionMatrix.GetDefaultsForRole(RolUsuario.Auditor);

        Assert.Equal(Enum.GetValues<PermisoSistema>().Length, admin.Count);
        Assert.DoesNotContain(PermisoSistema.PurgarTablasPesaje, supervisor);
        Assert.DoesNotContain(PermisoSistema.AdministrarSeguridad, supervisor);
        Assert.DoesNotContain(PermisoSistema.EditarPreciosReales, operador);
        Assert.Contains(PermisoSistema.VerAuditoria, auditor);
        Assert.DoesNotContain(PermisoSistema.ImportarFirebird, auditor);
    }
}
