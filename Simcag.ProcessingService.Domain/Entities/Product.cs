using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Simcag.ProcessingService.Domain.Entities
{
    public class Product
    {
        public Guid Id { get; private set; }
        public string ExternalId { get; private set; } = null!;
        public string Name { get; private set; } = null!;
        public string NormalizedName { get; private set; } = null!;
        public decimal Price { get; private set; }
        public string Source { get; private set; } = null!;
        public string? Category { get; private set; }
        public DateTime CollectionDate { get; private set; }
        public decimal? MarketBenchmarkPrice { get; private set; }
        public decimal? MarketDeviationPercentage { get; private set; }
        public string? BenchmarkSource { get; private set; }
        public DateTime? LastBenchmarkAt { get; private set; }
        /// <summary>Chave de agrupamento do catálogo (mesma regra que <c>ListProductCatalog</c>).</summary>
        public string CatalogNormalizedName { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        // EF Core constructor
        protected Product() { }

        private Product(
            string externalId,
            string name,
            decimal price,
            string source,
            string? category,
            DateTime collectionDate,
            string catalogNormalizedName)
        {
            Id = Guid.NewGuid();
            ExternalId = externalId;
            Name = name;
            NormalizedName = NormalizeName(name);
            CatalogNormalizedName = catalogNormalizedName;
            Price = price;
            Source = source;
            Category = category;
            CollectionDate = collectionDate;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;

            Validate();
        }

        public static Product Create(
            string externalId,
            string name,
            decimal price,
            string source,
            string? category,
            DateTime collectionDate,
            string catalogNormalizedName)
        {
            return new Product(externalId, name, price, source, category, collectionDate, catalogNormalizedName);
        }

        public void Update(
            string name,
            decimal price,
            string source,
            string? category,
            DateTime collectionDate,
            string catalogNormalizedName)
        {
            Name = name;
            NormalizedName = NormalizeName(name);
            CatalogNormalizedName = catalogNormalizedName;
            Price = price;
            Source = source;
            Category = category;
            CollectionDate = collectionDate;
            UpdatedAt = DateTime.UtcNow;

            Validate();
        }

        /// <summary>Atualiza benchmark de mercado quando a análise de preço retorna referência externa válida.</summary>
        public void UpdateMarketBenchmark(
            decimal marketBenchmarkPrice,
            decimal marketDeviationPercentage,
            string? benchmarkSource,
            DateTime benchmarkAt)
        {
            if (marketBenchmarkPrice <= 0m)
                throw new InvalidOperationException("Benchmark de mercado deve ser maior que zero.");

            MarketBenchmarkPrice = marketBenchmarkPrice;
            MarketDeviationPercentage = marketDeviationPercentage;
            BenchmarkSource = string.IsNullOrWhiteSpace(benchmarkSource) ? null : benchmarkSource.Trim();
            LastBenchmarkAt = benchmarkAt;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Normalização alinhada à coluna <c>NormalizedName</c> (dedupe / catálogo).</summary>
        public static string ComputeNormalizedName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            var normalized = rawName.Trim();
            normalized = Regex.Replace(normalized, @"\s+", " ");
            normalized = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
            normalized = Regex.Replace(normalized, @"[^a-zA-Z0-9\s\-]", "");

            return normalized.Trim().Replace(" ", "-");
        }

        private static string NormalizeName(string rawName) => ComputeNormalizedName(rawName);

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(ExternalId))
                throw new InvalidOperationException("ExternalId é obrigatório");
            
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Nome do produto é obrigatório");
            
            if (Price <= 0)
                throw new InvalidOperationException("Preço deve ser maior que zero");
            
            if (string.IsNullOrWhiteSpace(Source))
                throw new InvalidOperationException("Fonte é obrigatória");
            
            if (CollectionDate > DateTime.UtcNow.AddMinutes(5))
                throw new InvalidOperationException("Data de coleta não pode ser no futuro");
        }
    }
}