using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Simcag.ProcessingService.Domain.Entities
{
    public class Product
    {
        public Guid Id { get; private set; }
        public string ExternalId { get; private set; }
        public string Name { get; private set; }
        public string NormalizedName { get; private set; }
        public decimal Price { get; private set; }
        public string Source { get; private set; }
        public string? Category { get; private set; }
        public DateTime CollectionDate { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        // EF Core constructor
        protected Product() { }

        private Product(string externalId, string name, decimal price, string source, string? category, DateTime collectionDate)
        {
            Id = Guid.NewGuid();
            ExternalId = externalId;
            Name = name;
            NormalizedName = NormalizeName(name);
            Price = price;
            Source = source;
            Category = category;
            CollectionDate = collectionDate;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;

            Validate();
        }

        public static Product Create(string externalId, string name, decimal price, string source, string? category, DateTime collectionDate)
        {
            return new Product(externalId, name, price, source, category, collectionDate);
        }

        public void Update(string name, decimal price, string source, string? category, DateTime collectionDate)
        {
            Name = name;
            NormalizedName = NormalizeName(name);
            Price = price;
            Source = source;
            Category = category;
            CollectionDate = collectionDate;
            UpdatedAt = DateTime.UtcNow;
            
            Validate();
        }

        private static string NormalizeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            var normalized = rawName.Trim();
            normalized = Regex.Replace(normalized, @"\s+", " ");
            normalized = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
            normalized = Regex.Replace(normalized, @"[^a-zA-Z0-9\s\-]", "");
            
            return normalized.Trim().Replace(" ", "-");
        }

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