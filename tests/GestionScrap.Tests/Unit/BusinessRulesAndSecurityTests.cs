using GestionScrap.Application.DTOs;
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
    [InlineData("76218764-5", "96781200-K", false)]
    [InlineData("", "76218764-5", false)]
    public void RutChileno_MatchesExpectedVariants(string left, string right, bool expected)
    {
        Assert.Equal(expected, RutChileno.Matches(left, right));
    }

    [Fact]
    public void IsDuplicatePesaje_HandlesTalcahuanoHoraAndOtherPlantsFolio()
    {
        Pesaje[] existing =
        [
            new() { Planta = Planta.Santiago, Folio = 1001, HoraIngreso = new TimeOnly(8, 30) },
            new() { Planta = Planta.Talcahuano, Folio = 2002, HoraIngreso = new TimeOnly(9, 15) }
        ];

        Assert.True(BusinessRules.IsDuplicatePesaje(existing, 1001, Planta.Santiago, new TimeOnly(14, 0)));
        Assert.False(BusinessRules.IsDuplicatePesaje(existing, 2002, Planta.Talcahuano, new TimeOnly(10, 0)));
        Assert.True(BusinessRules.IsDuplicatePesaje(existing, 2002, Planta.Talcahuano, new TimeOnly(9, 15)));
    }

    [Fact]
    public void BuildMaterialTreeReport_AggregatesHierarchyAndUnassignedCodes()
    {
        MaterialGroupNode[] nodes =
        [
            new() { Id = 1, ParentId = null, Nombre = "SCRAP SUPERMERCADO", TipoNodo = NodoGrupoTipo.CarpetaGrupo, Orden = 1 },
            new() { Id = 2, ParentId = 1, Nombre = "Subgrupo Mix", TipoNodo = NodoGrupoTipo.CarpetaGrupo, Orden = 1 },
            new() { Id = 3, ParentId = 2, Nombre = "5300000009 · MIX", TipoNodo = NodoGrupoTipo.ProductoAsignado, CodigoProducto = "5300000009", Orden = 1 }
        ];

        Pesaje[] pesajes =
        [
            new() { Folio = 1, CodProducto = "5300000009", NomProducto = "SCRAP MIX SUPERMERCADO", PesoNeto = 12_000, FechaIngreso = new DateOnly(2026, 11, 10) },
            new() { Folio = 2, CodProducto = "NEWCODE01", NomProducto = "MATERIAL NUEVO", PesoNeto = 3_000, FechaIngreso = new DateOnly(2026, 11, 11) }
        ];

        Producto[] catalog =
        [
            new() { Codigo = "5300000009", Descripcion = "SCRAP MIX SUPERMERCADO" },
            new() { Codigo = "NEWCODE01", Descripcion = "MATERIAL NUEVO" }
        ];

        GrupoMaterialReportResultDto rep = BusinessRules.BuildMaterialTreeReport("Test", nodes, pesajes, catalog);

        Assert.Equal(15_000, rep.TotalKilos);
        Assert.Equal(1, rep.ProductosSinClasificarCount);
        Assert.Equal(12_000, rep.Filas.First(r => r.NodeId == 1).TotalKilos);
        Assert.Contains(rep.Filas, r => r.NodeId == -1 && r.TotalKilos == 3_000);
    }

    [Fact]
    public void BuildAnalisisProveedores_ComputesParetoAbcAndSafeVariance()
    {
        DateOnly hoy = new(2026, 11, 26);
        Pesaje[] actuales =
        [
            new() { RutCliente = "89201400", NomCliente = "ENV. CORDILLERA", PesoNeto = 80_000, CodProducto = "5300000009", NomProducto = "MIX", FechaIngreso = hoy.AddDays(-2) },
            new() { RutCliente = "77774598", NomCliente = "GIAIER SPA", PesoNeto = 15_000, CodProducto = "5302000004", NomProducto = "STRETCH", FechaIngreso = hoy.AddDays(-5) },
            new() { RutCliente = "11697166", NomCliente = "OSCAR SAAVEDRA", PesoNeto = 5_000, CodProducto = "5301002001", NomProducto = "COLOR", FechaIngreso = hoy.AddDays(-10) }
        ];
        Pesaje[] anteriores =
        [
            new() { RutCliente = "89201400", NomCliente = "ENV. CORDILLERA", PesoNeto = 40_000, FechaIngreso = hoy.AddMonths(-1) }
        ];

        AnalisisProveedoresResultDto res = BusinessRules.BuildAnalisisProveedores("Periodo", hoy, actuales, anteriores, []);

        Assert.Equal(3, res.TotalProveedoresActivos);
        Assert.Equal(100_000, res.TotalKilos);
        Assert.Equal("A", res.Ranking[0].ClaseAbc);
        Assert.Equal(100.0, res.Ranking[0].VariacionPorcentual);
        Assert.Equal("B", res.Ranking[1].ClaseAbc);
        Assert.Equal("C", res.Ranking[2].ClaseAbc);
    }

    [Fact]
    public void PasswordSecurity_ValidatesCambias0ComplexityAndHashing()
    {
        Assert.True(PasswordSecurity.ValidateComplexity("Cambias0").Valid);
        Assert.False(PasswordSecurity.ValidateComplexity("corta1").Valid);

        (string h, string s) = PasswordSecurity.HashPassword("Cambias0");
        Assert.True(PasswordSecurity.VerifyPassword("Cambias0", h, s));
        Assert.False(PasswordSecurity.VerifyPassword("otra1234", h, s));
    }

    [Fact]
    public void DefaultRoleProfiles_AdministratorContainsAllPermissions()
    {
        PerfilUsuario admin = DefaultRoleProfiles.GetSeededProfiles().First(p => p.Id == 1);
        Assert.Equal(Enum.GetValues<PermisoSistema>().Length, admin.Permisos.Count);
    }
}
