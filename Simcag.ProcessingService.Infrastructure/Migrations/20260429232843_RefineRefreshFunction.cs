using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <summary>
    /// Refina <c>refresh_monthly_expense_summary()</c> para usar <c>CONCURRENTLY</c> de forma
    /// determinística:
    ///
    /// - 1ª chamada (MView ainda não populada): <c>REFRESH MATERIALIZED VIEW</c> tradicional.
    ///   Necessário porque <c>CONCURRENTLY</c> exige que a MView já tenha sido populada uma vez.
    /// - Chamadas subsequentes: <c>CONCURRENTLY</c>, sem bloquear leitores.
    ///
    /// Substitui o <c>EXCEPTION WHEN OTHERS</c> que silenciava erros legítimos da MView.
    /// </summary>
    public partial class RefineRefreshFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION refresh_monthly_expense_summary() RETURNS void
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    is_populated boolean;
                BEGIN
                    SELECT relispopulated INTO is_populated
                    FROM pg_class
                    WHERE relname = 'mv_monthly_expense_summary' AND relkind = 'm';

                    IF is_populated IS NULL THEN
                        RAISE EXCEPTION 'Materialized view mv_monthly_expense_summary não existe.';
                    END IF;

                    IF is_populated THEN
                        REFRESH MATERIALIZED VIEW CONCURRENTLY mv_monthly_expense_summary;
                    ELSE
                        REFRESH MATERIALIZED VIEW mv_monthly_expense_summary;
                    END IF;
                END;
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura a versão da migration anterior (com EXCEPTION WHEN OTHERS).
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
    }
}
