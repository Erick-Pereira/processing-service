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
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();

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

                entity.HasIndex(p => new { p.ExternalId, p.Source }).IsUnique();

                entity.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(p => p.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<Expense>(entity =>
            {
                entity.ToTable("Expenses");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CondominioId).IsRequired();
                entity.Property(e => e.RawDocumentId).IsRequired();
                entity.Property(e => e.SupplierId).IsRequired();
                entity.Property(e => e.Category).IsRequired().HasMaxLength(120);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(8);
                entity.Property(e => e.Date).IsRequired();
                entity.Property(e => e.Region).HasMaxLength(80);
                entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(4,3)");
                entity.Property(e => e.LowConfidence);
                entity.Property(e => e.RawText).HasColumnType("text");
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.HasIndex(e => new { e.CondominioId, e.Date });
                entity.HasIndex(e => new { e.CondominioId, e.Category });
                entity.HasIndex(e => e.RawDocumentId).IsUnique();
            });

            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.ToTable("Suppliers");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.CondominioId);
                entity.Property(s => s.Cnpj).HasMaxLength(14);
                entity.Property(s => s.NormalizedName).IsRequired().HasMaxLength(200);
                entity.Property(s => s.Category).HasMaxLength(120);
                entity.Property(s => s.IsActive).IsRequired();
                entity.Property(s => s.CreatedAt).IsRequired();
                entity.Property(s => s.UpdatedAt).IsRequired();
                entity.HasIndex(s => s.Cnpj).IsUnique().HasFilter("\"Cnpj\" IS NOT NULL");
                entity.HasIndex(s => new { s.CondominioId, s.NormalizedName });
            });
        }
    }
}
