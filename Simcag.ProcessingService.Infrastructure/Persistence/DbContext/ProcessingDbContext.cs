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
    public DbSet<MessageOutbox> MessageOutboxes => Set<MessageOutbox>();
    public DbSet<ConsumerInboxRecord> ConsumerInboxRecords => Set<ConsumerInboxRecord>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OperationalInsightSnapshot> OperationalInsightSnapshots => Set<OperationalInsightSnapshot>();
    public DbSet<ExpenseComplianceFinding> ExpenseComplianceFindings => Set<ExpenseComplianceFinding>();
    public DbSet<ExpenseComplianceComment> ExpenseComplianceComments => Set<ExpenseComplianceComment>();

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
        ConfigureOperationalInsightSnapshot(modelBuilder);
        ConfigureMessageOutbox(modelBuilder);
        ConfigureConsumerInbox(modelBuilder);
        ConfigureExpenseComplianceFinding(modelBuilder);
        ConfigureExpenseComplianceComment(modelBuilder);

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
                .HasMaxLength(24)
                .IsRequired();
            e.Property(x => x.ProcessingStatus)
                .HasColumnName("processing_status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            e.Property(x => x.ApprovalStatus)
                .HasColumnName("approval_status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            e.Property(x => x.SettlementStatus)
                .HasColumnName("settlement_status")
                .HasConversion<string>()
                .HasMaxLength(24)
                .IsRequired();
            e.Property(x => x.ProcessingFailureReason).HasColumnName("processing_failure_reason").HasMaxLength(2000);
            e.Property(x => x.ProcessingFailedAt).HasColumnName("processing_failed_at");
            e.Property(x => x.ProcessingRetryCount).HasColumnName("processing_retry_count").IsRequired();
            e.Property(x => x.LastPipelineTransitionAt).HasColumnName("last_pipeline_transition_at");
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

    private static void ConfigureOperationalInsightSnapshot(ModelBuilder mb)
    {
        mb.Entity<OperationalInsightSnapshot>(e =>
        {
            e.ToTable("operational_insight_snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.RuleSetVersion).HasColumnName("rule_set_version").IsRequired().HasMaxLength(64);
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            e.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
            e.Property(x => x.PayloadJson).HasColumnName("payload_json").IsRequired().HasColumnType("jsonb");
            e.Property(x => x.ContextJson).HasColumnName("context_json").HasColumnType("jsonb");

            e.HasIndex(x => new { x.TenantId, x.RuleSetVersion, x.ExpiresAtUtc })
                .HasDatabaseName("ix_insight_snapshots_tenant_rule_expires");
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc })
                .HasDatabaseName("ix_insight_snapshots_tenant_created");
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
        mb.Entity<OperationalInsightSnapshot>().HasQueryFilter(x => x.TenantId == CurrentTenantId());
        mb.Entity<ExpenseComplianceFinding>().HasQueryFilter(x => x.TenantId == CurrentTenantId());
        mb.Entity<ExpenseComplianceComment>().HasQueryFilter(x => x.TenantId == CurrentTenantId());
        mb.Entity<ConsumerInboxRecord>().HasQueryFilter(x => x.TenantId == CurrentTenantId());
        // ExpenseItem é filtrado via Expense (cascade pelo include). Sem filtro próprio.
    }

    private static void ConfigureMessageOutbox(ModelBuilder mb)
    {
        mb.Entity<MessageOutbox>(e =>
        {
            e.ToTable("message_outbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
            e.HasIndex(x => x.MessageId).IsUnique().HasDatabaseName("ux_message_outbox_message_id");
            e.Property(x => x.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(256);
            e.HasIndex(x => new { x.TenantId, x.DedupeKey })
                .IsUnique()
                .HasDatabaseName("ux_message_outbox_tenant_dedupe")
                .HasFilter("dedupe_key IS NOT NULL");
            e.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(200);
            e.Property(x => x.RoutingKey).HasColumnName("routing_key").IsRequired().HasMaxLength(200);
            e.Property(x => x.PayloadJson).HasColumnName("payload_json").IsRequired().HasColumnType("jsonb");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(200);
            e.Property(x => x.TraceParent).HasColumnName("trace_parent").HasMaxLength(256);
            e.Property(x => x.TraceState).HasColumnName("trace_state").HasMaxLength(256);
            e.Property(x => x.Baggage).HasColumnName("baggage").HasMaxLength(4000);
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            e.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
            e.Property(x => x.MaxAttempts).HasColumnName("max_attempts").IsRequired();
            e.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc").IsRequired();
            e.Property(x => x.LockedUntilUtc).HasColumnName("locked_until_utc");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            e.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");
            e.Property(x => x.PoisonedAtUtc).HasColumnName("poisoned_at_utc");
            e.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000);
            e.HasIndex(x => new { x.Status, x.NextAttemptAtUtc }).HasDatabaseName("ix_message_outbox_status_next_attempt");
        });
    }

    private static void ConfigureConsumerInbox(ModelBuilder mb)
    {
        mb.Entity<ConsumerInboxRecord>(e =>
        {
            e.ToTable("consumer_inbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.ConsumerGroup).HasColumnName("consumer_group").IsRequired().HasMaxLength(128);
            e.Property(x => x.TransportMessageId).HasColumnName("transport_message_id").IsRequired();
            e.Property(x => x.DomainEventId).HasColumnName("domain_event_id");
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            e.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc").IsRequired();
            e.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
            e.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
            e.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000);
            e.HasIndex(x => new { x.ConsumerGroup, x.TransportMessageId })
                .IsUnique()
                .HasDatabaseName("ux_consumer_inbox_group_transport");
            e.HasIndex(x => new { x.TenantId, x.ConsumerGroup, x.ReceivedAtUtc })
                .HasDatabaseName("ix_consumer_inbox_tenant_group_received");
        });
    }

    private static void ConfigureExpenseComplianceFinding(ModelBuilder mb)
    {
        mb.Entity<ExpenseComplianceFinding>(e =>
        {
            e.ToTable("expense_compliance_findings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.ExpenseId).HasColumnName("expense_id").IsRequired();
            e.Property(x => x.RuleCode).HasColumnName("rule_code").IsRequired().HasMaxLength(64);
            e.Property(x => x.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasColumnName("description").IsRequired().HasMaxLength(2000);
            e.Property(x => x.Severity).HasColumnName("severity").IsRequired().HasMaxLength(16);
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(16);
            e.Property(x => x.Origin).HasColumnName("origin").IsRequired().HasMaxLength(16);
            e.Property(x => x.Confidence).HasColumnName("confidence").HasColumnType("numeric(4,3)");
            e.Property(x => x.DetailJson).HasColumnName("detail_json").HasColumnType("jsonb");
            e.Property(x => x.EvidenceDocumentIdsJson).HasColumnName("evidence_document_ids_json").HasColumnType("jsonb");
            e.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc").IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            e.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            e.Property(x => x.WaivedAtUtc).HasColumnName("waived_at_utc");
            e.Property(x => x.WaivedByUserId).HasColumnName("waived_by_user_id");
            e.Property(x => x.WaivedByUserName).HasColumnName("waived_by_user_name").HasMaxLength(200);
            e.Property(x => x.WaivedReason).HasColumnName("waived_reason").HasMaxLength(2000);
            e.HasIndex(x => new { x.TenantId, x.ExpenseId, x.RuleCode })
                .IsUnique()
                .HasDatabaseName("ux_expense_compliance_tenant_expense_rule");
            e.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_expense_compliance_tenant_status");
        });
    }

    private static void ConfigureExpenseComplianceComment(ModelBuilder mb)
    {
        mb.Entity<ExpenseComplianceComment>(e =>
        {
            e.ToTable("expense_compliance_comments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.FindingId).HasColumnName("finding_id").IsRequired();
            e.Property(x => x.ExpenseId).HasColumnName("expense_id").IsRequired();
            e.Property(x => x.Body).HasColumnName("body").IsRequired().HasMaxLength(4000);
            e.Property(x => x.AuthorUserId).HasColumnName("author_user_id");
            e.Property(x => x.AuthorUserName).HasColumnName("author_user_name").HasMaxLength(200);
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            e.HasIndex(x => x.FindingId).HasDatabaseName("ix_expense_compliance_comments_finding_id");
            e.HasIndex(x => new { x.TenantId, x.ExpenseId }).HasDatabaseName("ix_expense_compliance_comments_tenant_expense");
        });
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
