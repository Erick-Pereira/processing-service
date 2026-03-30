using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence;

public class ProcessingDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public ProcessingDbContext(DbContextOptions<ProcessingDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.NormalizedName);
            entity.HasIndex(e => e.Source);
            entity.HasIndex(e => e.CollectionDate);
        });
    }
}
