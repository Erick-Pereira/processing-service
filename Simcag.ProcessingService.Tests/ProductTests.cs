using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Tests.Helpers;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Simcag.ProcessingService.Tests
{
    public class ProductTests
    {
        [Fact]
        public void Product_Create_WithValidData_ShouldCreateSuccessfully()
        {
            // Arrange
            var externalId = "EXT001";
            var name = "Produto Teste";
            var price = 99.9m;
            var source = "Teste";
            var category = "Categoria Teste";
            var collectionDate = DateTime.UtcNow;

            // Act
            var product = Product.Create(externalId, name, price, source, category, collectionDate);

            // Assert
            Assert.NotNull(product);
            Assert.NotEqual(Guid.Empty, product.Id);
            Assert.Equal(externalId, product.ExternalId);
            Assert.Equal(name, product.Name);
            Assert.NotEmpty(product.NormalizedName);
            Assert.Equal(price, product.Price);
            Assert.Equal(source, product.Source);
            Assert.Equal(category, product.Category);
            Assert.Equal(collectionDate, product.CollectionDate);
            Assert.True(product.CreatedAt <= DateTime.UtcNow);
            Assert.True(product.UpdatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void Product_Create_WithInvalidExternalId_ShouldThrowException()
        {
            // Arrange
            var externalId = "";
            var name = "Produto Teste";
            var price = 99.9m;
            var source = "Teste";
            var category = "Categoria Teste";
            var collectionDate = DateTime.UtcNow;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => Product.Create(externalId, name, price, source, category, collectionDate));
        }

        [Fact]
        public void Product_Create_WithInvalidName_ShouldThrowException()
        {
            // Arrange
            var externalId = "EXT001";
            var name = "";
            var price = 99.9m;
            var source = "Teste";
            var category = "Categoria Teste";
            var collectionDate = DateTime.UtcNow;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => Product.Create(externalId, name, price, source, category, collectionDate));
        }

        [Fact]
        public void Product_Create_WithInvalidPrice_ShouldThrowException()
        {
            // Arrange
            var externalId = "EXT001";
            var name = "Produto Teste";
            var price = 0m;
            var source = "Teste";
            var category = "Categoria Teste";
            var collectionDate = DateTime.UtcNow;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => Product.Create(externalId, name, price, source, category, collectionDate));
        }

        [Fact]
        public void Product_Create_WithInvalidSource_ShouldThrowException()
        {
            // Arrange
            var externalId = "EXT001";
            var name = "Produto Teste";
            var price = 99.9m;
            var source = "";
            var category = "Categoria Teste";
            var collectionDate = DateTime.UtcNow;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => Product.Create(externalId, name, price, source, category, collectionDate));
        }

        [Fact]
        public void Product_Update_WithValidData_ShouldUpdateSuccessfully()
        {
            // Arrange
            var product = Product.Create("EXT001", "Produto Original", 99.9m, "Teste", "Categoria", DateTime.UtcNow);
            var newName = "Produto Atualizado";
            var newPrice = 199.9m;
            var newSource = "Atualizado";
            var newCategory = "Categoria Atualizada";
            var collectionDate = DateTime.UtcNow;

            // Act
            product.Update(newName, newPrice, newSource, newCategory, collectionDate);

            // Assert
            Assert.Equal(newName, product.Name);
            Assert.NotEmpty(product.NormalizedName);
            Assert.Equal(newPrice, product.Price);
            Assert.Equal(newSource, product.Source);
            Assert.Equal(newCategory, product.Category);
            Assert.True(product.UpdatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void Product_NormalizeName_WithSpecialCharacters_ShouldReturnNormalizedString()
        {
            // Arrange
            var rawName = "  Produto   com   espaços   e   caracteres especiais!@#$%  ";
            var expected = "Produto-com-espaços-e-caracteres-especiais";

            // Act
            var normalized = Product.NormalizeName(rawName);

            // Assert
            Assert.Equal(expected, normalized);
        }

        [Fact]
        public void Product_NormalizeName_WithEmptyString_ShouldReturnEmptyString()
        {
            // Arrange
            var rawName = "";
            var expected = "";

            // Act
            var normalized = Product.NormalizeName(rawName);

            // Assert
            Assert.Equal(expected, normalized);
        }
    }
}