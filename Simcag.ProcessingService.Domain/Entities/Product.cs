namespace Simcag.ProcessingService.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CollectionDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}