using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <summary>
    /// Despesas canceladas (4) ou rejeitadas (3) não devem contribuir para <c>outstanding</c>
    /// na MView do dashboard nem inflar KPIs de valor em aberto.
    /// </summary>
    public partial class FixOutstandingForClosedExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    COALESCE(SUM(
                        CASE
                            WHEN e.approval_status IN (3, 4) THEN 0
                            ELSE e.total_amount - COALESCE(p.amount_paid, 0)
                        END
                    ), 0)                                                      AS outstanding
                FROM expenses e
                LEFT JOIN paid p ON p.expense_id = e.id
                WHERE e.deleted_at IS NULL
                GROUP BY e.tenant_id, year, month, e.category, e.supplier_id;

                CREATE UNIQUE INDEX ux_mv_monthly_expense_summary
                    ON mv_monthly_expense_summary (tenant_id, year, month, category, supplier_id);

                CREATE INDEX ix_mv_monthly_expense_summary_tenant_year
                    ON mv_monthly_expense_summary (tenant_id, year);
            ");

            migrationBuilder.Sql("SELECT refresh_monthly_expense_summary();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql("SELECT refresh_monthly_expense_summary();");
        }
    }
}
