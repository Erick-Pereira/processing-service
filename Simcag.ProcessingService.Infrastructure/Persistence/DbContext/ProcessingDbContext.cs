using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Infrastructure.Auditing;
using Simcag.Shared.Auditing;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Infrastructure.Persistence;

public class ProcessingDbContext : DbContext
{
    private readonly ITenantContext? _tenant;
    private readonly ICurrentUserContext? _user;
    private readonly ProcessingAuditLogSink? _auditSink;

    /// <summary>
    /// Construtor usado por <c>dotnet ef</c> e por testes — sem tenant/user.
    /// Em produção, prefira o construtor com dependências injetadas.
    /// </summary>
    public ProcessingDbContext(DbContextOptions<ProcessingDbContext> options) : base(options)
    {
    }

    public ProcessingDbContext(
        DbContextOptions<ProcessingDbContext> options,
        ITenantContext tenant,
        ICurrentUserContext user,
        ProcessingAuditLogSink auditSink) : base(options)
    {
        _tenant = tenant;
        _user = user;
        _auditSink = auditSink;
    }

    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; } = null!;
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EventId).IsUnique();
            entity.Property(x => x.EventId).IsRequired();
            entity.Property(x => x.ProcessedAt).IsRequired();
        });

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

        ConfigureExpense(modelBuilder);
        ConfigureExpenseItem(modelBuilder);
        ConfigurePayment(modelBuilder);
        ConfigureSupplier(modelBuilder);
        ConfigureAuditLog(modelBuilder);

        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ConfigureExpense(ModelBuilder mb)
    {
        mb.Entity<Expense>(e =>
        {
            e.ToTable("expenses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
            e.Property(x => x.Description).HasColumnName("description").IsRequired().HasMaxLength(500);
            e.Property(x => x.Category).HasColumnName("category").IsRequired().HasMaxLength(120);
            e.Property(x => x.Currency).HasColumnName("currency").IsRequired().HasMaxLength(8);
            e.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            e.Property(x => x.RawDocumentId).HasColumnName("raw_document_id");
            e.Property(x => x.ConfidenceScore).HasColumnName("confidence_score").HasColumnType("numeric(4,3)");
            e.Property(x => x.LowConfidence).HasColumnName("low_confidence");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.Property(x => x.TotalAmount)
                .HasColumnName("total_amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            e.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Metadata.FindNavigation(nameof(Expense.Items))!.SetPropertyAccessMode(PropertyAccessMode.Field);

            e.HasMany(x => x.Payments)
                .WithOne()
                .HasForeignKey(p => p.ExpenseId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Metadata.FindNavigation(nameof(Expense.Payments))!.SetPropertyAccessMode(PropertyAccessMode.Field);

            e.HasIndex(x => new { x.TenantId, x.IssueDate }).HasDatabaseName("ix_expenses_tenant_issue_date");
            e.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_expenses_tenant_status");
            e.HasIndex(x => new { x.TenantId, x.Category }).HasDatabaseName("ix_expenses_tenant_category");
            e.HasIndex(x => x.RawDocumentId)
                .IsUnique()
                .HasFilter("raw_document_id IS NOT NULL")
                .HasDatabaseName("ux_expenses_raw_document_id");
        });
    }

    private static void ConfigureExpenseItem(ModelBuilder mb)
    {
        mb.Entity<ExpenseItem>(e =>
        {
            e.ToTable("expense_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ExpenseId).HasColumnName("expense_id").IsRequired();
            e.Property(x => x.Description).HasColumnName("description").IsRequired().HasMaxLength(500);
            e.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,4)").IsRequired();
            e.Property(x => x.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(18,4)").IsRequired();
            e.Property(x => x.TotalPrice)
                .HasColumnName("total_price")
                .HasColumnType("numeric(18,4)")
                .HasComputedColumnSql("\"quantity\" * \"unit_price\"", stored: true);

            e.HasIndex(x => x.ExpenseId).HasDatabaseName("ix_expense_items_expense_id");
        });
    }

    private static void ConfigurePayment(ModelBuilder mb)
    {
        mb.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.ExpenseId).HasColumnName("expense_id").IsRequired();
            e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
            e.Property(x => x.PaymentDate).HasColumnName("payment_date").IsRequired();
            e.Property(x => x.Method)
                .HasColumnName("method")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            e.Property(x => x.ReferenceCode).HasColumnName("reference_code").HasMaxLength(100);
            e.Property(x => x.IsRefunded).HasColumnName("is_refunded");
            e.Property(x => x.RefundedAt).HasColumnName("refunded_at");
            e.Property(x => x.RefundReason).HasColumnName("refund_reason").HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            e.HasIndex(x => new { x.TenantId, x.PaymentDate }).HasDatabaseName("ix_payments_tenant_date");
            e.HasIndex(x => x.ExpenseId).HasDatabaseName("ix_payments_expense_id");
        });
    }

    private static void ConfigureSupplier(ModelBuilder mb)
    {
        mb.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            e.Property(x => x.NormalizedName).HasColumnName("normalized_name").IsRequired().HasMaxLength(200);
            e.Property(x => x.Document).HasColumnName("document").IsRequired().HasMaxLength(14);
            e.Property(x => x.DocumentType)
                .HasColumnName("document_type")
                .HasConversion<string>()
                .HasMaxLength(8)
                .IsRequired();
            e.Property(x => x.Category).HasColumnName("category").HasMaxLength(120);
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

            e.OwnsOne(x => x.Contact, c =>
            {
                c.Property(p => p.Email).HasColumnName("contact_email").HasMaxLength(200);
                c.Property(p => p.Phone).HasColumnName("contact_phone").HasMaxLength(40);
                c.Property(p => p.Address).HasColumnName("contact_address").HasMaxLength(300);
            });
            e.Navigation(x => x.Contact).IsRequired();

            e.HasIndex(x => new { x.TenantId, x.Document })
                .IsUnique()
                .HasDatabaseName("ux_suppliers_tenant_document");
            e.HasIndex(x => new { x.TenantId, x.NormalizedName })
                .HasDatabaseName("ix_suppliers_tenant_name");
        });
    }

    private static void ConfigureAuditLog(ModelBuilder mb)
    {
        mb.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.EntityName).HasColumnName("entity_name").IsRequired().HasMaxLength(120);
            e.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();
            e.Property(x => x.Action).HasColumnName("action").IsRequired().HasMaxLength(16);
            e.Property(x => x.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
            e.Property(x => x.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
            e.Property(x => x.PerformedBy).HasColumnName("performed_by");
            e.Property(x => x.PerformedByName).HasColumnName("performed_by_name").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            e.HasIndex(x => new { x.TenantId, x.EntityName, x.EntityId }).HasDatabaseName("ix_audit_logs_tenant_entity");
            e.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("ix_audit_logs_tenant_created");
        });
    }

    private void ApplyTenantQueryFilters(ModelBuilder mb)
    {
        // Tenant atual capturado por closure: o filtro é avaliado por query, e EF re-avalia quando o valor muda.
        mb.Entity<Expense>().HasQueryFilter(x =>
            !x.DeletedAt.HasValue && x.TenantId == CurrentTenantId());
        mb.Entity<Payment>().HasQueryFilter(x => x.TenantId == CurrentTenantId());
        mb.Entity<Supplier>().HasQueryFilter(x => x.IsActive && x.TenantId == CurrentTenantId());
        mb.Entity<AuditLog>().HasQueryFilter(x => x.TenantId == CurrentTenantId());
        // ExpenseItem é filtrado via Expense (cascade pelo include). Sem filtro próprio.
    }

    /// <summary>
    /// Tenant atual; <see cref="Guid.Empty"/> quando o ITenantContext não foi injetado
    /// (cenários de migração / dotnet ef). Em runtime de API, sempre estará populado pelo middleware.
    /// </summary>
    private Guid CurrentTenantId() => _tenant?.TenantId ?? Guid.Empty;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantOnWrites();

        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await PersistAuditLogsAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private void EnforceTenantOnWrites()
    {
        if (_tenant is null) return;

        var current = _tenant.TenantId;
        var entries = ChangeTracker.Entries<IAuditableEntity>().ToList();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.TenantId == Guid.Empty && current != Guid.Empty)
                    {
                        entry.Property(nameof(IAuditableEntity.TenantId)).CurrentValue = current;
                    }
                    if (current != Guid.Empty && entry.Entity.TenantId != current)
                        throw new CrossTenantWriteException(entry.Entity.GetType().Name, current, entry.Entity.TenantId);
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    var original = (Guid)(entry.OriginalValues[nameof(IAuditableEntity.TenantId)] ?? Guid.Empty);
                    if (current != Guid.Empty && original != current)
                        throw new CrossTenantWriteException(entry.Entity.GetType().Name, current, original);
                    break;
            }
        }
    }

    private async Task PersistAuditLogsAsync(CancellationToken ct)
    {
        if (_auditSink is null) return;
        var entries = _auditSink.Drain();
        if (entries.Count == 0) return;

        var logs = new List<AuditLog>(entries.Count);
        foreach (var entry in entries)
        {
            var tenantId = entry.TenantId != Guid.Empty
                ? entry.TenantId
                : (_tenant?.TenantId ?? Guid.Empty);
            if (tenantId == Guid.Empty) continue;

            logs.Add(AuditLog.FromEntry(
                tenantId,
                entry.EntityName,
                entry.EntityId,
                entry.Action,
                entry.OldValue,
                entry.NewValue,
                entry.PerformedBy ?? _user?.UserId,
                entry.PerformedByName ?? _user?.UserName,
                entry.CreatedAt));
        }

        if (logs.Count == 0) return;
        await AuditLogs.AddRangeAsync(logs, ct).ConfigureAwait(false);
        await base.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
