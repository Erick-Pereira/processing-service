using Simcag.ProcessingService.Application.UseCases.Suppliers;
using Simcag.ProcessingService.Domain.Entities;
using Xunit;

namespace Simcag.ProcessingService.Tests;

public class SupplierUpsertPolicyTests
{
    [Fact]
    public void ShouldUpdateExisting_mesmo_nome_normalizado()
    {
        var existing = Supplier.Create(Guid.NewGuid(), "Empresa XYZ LTDA", "12345678000199");
        Assert.True(SupplierUpsertPolicy.ShouldUpdateExisting(existing, "EMPRESA XYZ LTDA"));
    }

    [Fact]
    public void ShouldUpdateExisting_nao_sobrescreve_razao_social_diferente()
    {
        var existing = Supplier.Create(Guid.NewGuid(), "SEGURANCA ELETRONICA BRASIL LTDA", "12345678000199");
        Assert.False(SupplierUpsertPolicy.ShouldUpdateExisting(existing, "EMPRESA XYZ SERVICOS LTDA"));
    }

    [Fact]
    public void BuildSyntheticDocumentForName_e_deterministico()
    {
        var a = SupplierUpsertPolicy.BuildSyntheticDocumentForName(Supplier.NormalizeName("Empresa A"));
        var b = SupplierUpsertPolicy.BuildSyntheticDocumentForName(Supplier.NormalizeName("Empresa A"));
        var c = SupplierUpsertPolicy.BuildSyntheticDocumentForName(Supplier.NormalizeName("Empresa B"));

        Assert.Equal(14, a.Length);
        Assert.StartsWith("88", a);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
