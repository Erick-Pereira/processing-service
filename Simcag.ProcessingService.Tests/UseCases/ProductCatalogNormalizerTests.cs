using Simcag.ProcessingService.Application.UseCases.Products;
using Xunit;

namespace Simcag.ProcessingService.Tests.UseCases;

public sealed class ProductCatalogNormalizerTests
{
    [Theory]
    [InlineData("Câmera IP 2MP", "camera-ip-2mp")]
    [InlineData("  Material de manutenção  ", "material-de-manutencao")]
    [InlineData("", "sem-descricao")]
    public void Normalize_produces_stable_catalog_key(string input, string expected)
    {
        Assert.Equal(expected, ProductCatalogNormalizer.Normalize(input));
    }
}
