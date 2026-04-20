using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Infrastructure.Services;

namespace Simcag.ProcessingService.Infrastructure.Persistence
{
    public class ProcessingDbContext : DbContext
    {
        public ProcessingDbContext(DbContextOptions<ProcessingDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProcessedEvent> ProcessedEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ProcessedEvent for idempotency
            modelBuilder.Entity<ProcessedEvent>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.EventId).IsUnique();
                entity.Property(x => x.EventId).IsRequired();
                entity.Property(x => x.ProcessedAt).IsRequired();
            });

            // Configure Product entity
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.ExternalId).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Name).IsRequired().HasMaxLength(500);
                entity.Property(p => p.NormalizedName).IsRequired().HasMaxLength(500);
                entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
                entity.Property(p => p.Source).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Category).HasMaxLength(200);

                // Index for idempotency (ExternalId + Source)
                entity.HasIndex(p => new { p.ExternalId, p.Source }).IsUnique();

                entity.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(p => p.UpdatedAt).HasDefaultValueSql("NOW()");
            });
        }
    }
}
