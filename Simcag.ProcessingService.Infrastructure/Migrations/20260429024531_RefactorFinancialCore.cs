using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorFinancialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Suppliers",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Cnpj",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CondominioId_NormalizedName",
                table: "Suppliers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Expenses",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_RawDocumentId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CondominioId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "RawText",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Expenses");

            migrationBuilder.RenameTable(
                name: "Suppliers",
                newName: "suppliers");

            migrationBuilder.RenameTable(
                name: "Expenses",
                newName: "expenses");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "suppliers",
                newName: "category");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "suppliers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "suppliers",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "NormalizedName",
                table: "suppliers",
                newName: "normalized_name");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "suppliers",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "suppliers",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "expenses",
                newName: "currency");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "expenses",
                newName: "category");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "expenses",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "expenses",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "expenses",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "RawDocumentId",
                table: "expenses",
                newName: "raw_document_id");

            migrationBuilder.RenameColumn(
                name: "LowConfidence",
                table: "expenses",
                newName: "low_confidence");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "expenses",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ConfidenceScore",
                table: "expenses",
                newName: "confidence_score");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "expenses",
                newName: "issue_date");

            migrationBuilder.RenameColumn(
                name: "CondominioId",
                table: "expenses",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "expenses",
                newName: "total_amount");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_CondominioId_Date",
                table: "expenses",
                newName: "ix_expenses_tenant_issue_date");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_CondominioId_Category",
                table: "expenses",
                newName: "ix_expenses_tenant_category");

            migrationBuilder.AddColumn<string>(
                name: "contact_address",
                table: "suppliers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "suppliers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document",
                table: "suppliers",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "document_type",
                table: "suppliers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "suppliers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "raw_document_id",
                table: "expenses",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<decimal>(
                name: "confidence_score",
                table: "expenses",
                type: "numeric(4,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(4,3)");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "expenses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "due_date",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "expenses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_suppliers",
                table: "suppliers",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_expenses",
                table: "expenses",
                column: "id");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    performed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_price = table.Column<decimal>(type: "numeric(18,4)", nullable: false, computedColumnSql: "\"quantity\" * \"unit_price\"", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_items_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_refunded = table.Column<bool>(type: "boolean", nullable: false),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refund_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_tenant_name",
                table: "suppliers",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ux_suppliers_tenant_document",
                table: "suppliers",
                columns: new[] { "tenant_id", "document" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expenses_tenant_status",
                table: "expenses",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_expenses_raw_document_id",
                table: "expenses",
                column: "raw_document_id",
                unique: true,
                filter: "raw_document_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_created",
                table: "audit_logs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_entity",
                table: "audit_logs",
                columns: new[] { "tenant_id", "entity_name", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_items_expense_id",
                table: "expense_items",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_expense_id",
                table: "payments",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_date",
                table: "payments",
                columns: new[] { "tenant_id", "payment_date" });

            // ===========================================================================
            // BACKFILL: para cada Expense legada (que veio do schema antigo com Amount),
            // gerar 1 ExpenseItem com Quantity=1 e UnitPrice=total_amount, e definir
            // status default 'Pending', description default vazia.
            // Idempotente: só insere se ainda não houver itens.
            // ===========================================================================
            migrationBuilder.Sql(@"
                UPDATE expenses
                SET status = 'Pending'
                WHERE status = '' OR status IS NULL;

                UPDATE expenses
                SET description = COALESCE(NULLIF(description, ''), category)
                WHERE description IS NULL OR description = '';

                INSERT INTO expense_items (id, expense_id, description, quantity, unit_price)
                SELECT gen_random_uuid(), e.id, e.description, 1, COALESCE(e.total_amount, 0)
                FROM expenses e
                LEFT JOIN expense_items i ON i.expense_id = e.id
                WHERE i.id IS NULL AND COALESCE(e.total_amount, 0) > 0;
            ");

            // ===========================================================================
            // MATERIALIZED VIEW: agregação mensal por categoria + fornecedor.
            // O índice único permite REFRESH MATERIALIZED VIEW CONCURRENTLY.
            // ===========================================================================
            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW IF NOT EXISTS mv_monthly_expense_summary AS
                SELECT
                    e.tenant_id                                                AS tenant_id,
                    EXTRACT(YEAR  FROM e.issue_date)::int                      AS year,
                    EXTRACT(MONTH FROM e.issue_date)::int                      AS month,
                    e.category                                                 AS category,
                    e.supplier_id                                              AS supplier_id,
                    COUNT(DISTINCT e.id)::int                                  AS expense_count,
                    COALESCE(SUM(e.total_amount), 0)                           AS total_amount,
                    COALESCE((
                        SELECT SUM(p.amount)
                        FROM payments p
                        WHERE p.expense_id = e.id AND p.is_refunded = FALSE
                    ), 0)                                                      AS total_paid_per_expense,
                    COALESCE(SUM(e.total_amount), 0) - COALESCE((
                        SELECT SUM(p.amount)
                        FROM payments p
                        WHERE p.expense_id = e.id AND p.is_refunded = FALSE
                    ), 0)                                                      AS outstanding_per_expense
                FROM expenses e
                WHERE e.deleted_at IS NULL
                GROUP BY e.tenant_id, year, month, e.category, e.supplier_id, e.id
                WITH NO DATA;
            ");

            // A MView acima fica granular por expense (necessário para subqueries de payments).
            // Recriamos como agregada por (tenant, year, month, category, supplier).
            migrationBuilder.Sql(@"
                DROP MATERIALIZED VIEW IF EXISTS mv_monthly_expense_summary;

                CREATE MATERIALIZED VIEW mv_monthly_expense_summary AS
                WITH paid AS (
                    SELECT p.expense_id, COALESCE(SUM(p.amount), 0) AS amount_paid
                    FROM payments p
                    WHERE p.is_refunded = FALSE
                    GROUP BY p.expense_id
                )
                SELECT
                    e.tenant_id                                                AS tenant_id,
                    EXTRACT(YEAR  FROM e.issue_date)::int                      AS year,
                    EXTRACT(MONTH FROM e.issue_date)::int                      AS month,
                    e.category                                                 AS category,
                    e.supplier_id                                              AS supplier_id,
                    COUNT(*)::int                                              AS expense_count,
                    COALESCE(SUM(e.total_amount), 0)                           AS total_amount,
                    COALESCE(SUM(p.amount_paid), 0)                            AS total_paid,
                    COALESCE(SUM(e.total_amount), 0) - COALESCE(SUM(p.amount_paid), 0)
                                                                               AS outstanding
                FROM expenses e
                LEFT JOIN paid p ON p.expense_id = e.id
                WHERE e.deleted_at IS NULL
                GROUP BY e.tenant_id, year, month, e.category, e.supplier_id;

                CREATE UNIQUE INDEX ux_mv_monthly_expense_summary
                    ON mv_monthly_expense_summary (tenant_id, year, month, category, supplier_id);

                CREATE INDEX ix_mv_monthly_expense_summary_tenant_year
                    ON mv_monthly_expense_summary (tenant_id, year);
            ");

            // ===========================================================================
            // FUNÇÃO SQL chamada pelo DashboardRefreshWorker (e endpoint admin).
            // CONCURRENTLY exige o índice único acima.
            // ===========================================================================
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION refresh_monthly_expense_summary() RETURNS void
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    BEGIN
                        REFRESH MATERIALIZED VIEW CONCURRENTLY mv_monthly_expense_summary;
                    EXCEPTION WHEN OTHERS THEN
                        REFRESH MATERIALIZED VIEW mv_monthly_expense_summary;
                    END;
                END;
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS refresh_monthly_expense_summary();");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_monthly_expense_summary;");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "expense_items");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_suppliers",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "ix_suppliers_tenant_name",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "ux_suppliers_tenant_document",
                table: "suppliers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_expenses",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "ix_expenses_tenant_status",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "ux_expenses_raw_document_id",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "contact_address",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "document",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "document_type",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "name",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "description",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "status",
                table: "expenses");

            migrationBuilder.RenameTable(
                name: "suppliers",
                newName: "Suppliers");

            migrationBuilder.RenameTable(
                name: "expenses",
                newName: "Expenses");

            migrationBuilder.RenameColumn(
                name: "category",
                table: "Suppliers",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Suppliers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Suppliers",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "normalized_name",
                table: "Suppliers",
                newName: "NormalizedName");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "Suppliers",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Suppliers",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "currency",
                table: "Expenses",
                newName: "Currency");

            migrationBuilder.RenameColumn(
                name: "category",
                table: "Expenses",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Expenses",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Expenses",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "Expenses",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "raw_document_id",
                table: "Expenses",
                newName: "RawDocumentId");

            migrationBuilder.RenameColumn(
                name: "low_confidence",
                table: "Expenses",
                newName: "LowConfidence");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Expenses",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "confidence_score",
                table: "Expenses",
                newName: "ConfidenceScore");

            migrationBuilder.RenameColumn(
                name: "total_amount",
                table: "Expenses",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "Expenses",
                newName: "CondominioId");

            migrationBuilder.RenameColumn(
                name: "issue_date",
                table: "Expenses",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "ix_expenses_tenant_issue_date",
                table: "Expenses",
                newName: "IX_Expenses_CondominioId_Date");

            migrationBuilder.RenameIndex(
                name: "ix_expenses_tenant_category",
                table: "Expenses",
                newName: "IX_Expenses_CondominioId_Category");

            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "Suppliers",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CondominioId",
                table: "Suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RawDocumentId",
                table: "Expenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ConfidenceScore",
                table: "Expenses",
                type: "numeric(4,3)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(4,3)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawText",
                table: "Expenses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Expenses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Suppliers",
                table: "Suppliers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Expenses",
                table: "Expenses",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Cnpj",
                table: "Suppliers",
                column: "Cnpj",
                unique: true,
                filter: "\"Cnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CondominioId_NormalizedName",
                table: "Suppliers",
                columns: new[] { "CondominioId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_RawDocumentId",
                table: "Expenses",
                column: "RawDocumentId",
                unique: true);
        }
    }
}
