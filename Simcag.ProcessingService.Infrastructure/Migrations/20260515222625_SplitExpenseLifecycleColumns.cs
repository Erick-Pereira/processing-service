using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitExpenseLifecycleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "expenses",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "expenses",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_pipeline_transition_at",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "processing_failed_at",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_failure_reason",
                table: "expenses",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "processing_retry_count",
                table: "expenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "processing_status",
                table: "expenses",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "settlement_status",
                table: "expenses",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE expenses AS e
                SET
                    processing_status = 'Completed',
                    approval_status = CASE e.status
                        WHEN 'Cancelled' THEN 'Cancelled'
                        WHEN 'Rejected' THEN 'Rejected'
                        WHEN 'Approved' THEN 'Approved'
                        WHEN 'Paid' THEN 'Approved'
                        WHEN 'ProcessingFailed' THEN 'PendingApproval'
                        WHEN 'Pending' THEN 'PendingApproval'
                        ELSE 'PendingApproval'
                    END,
                    settlement_status = CASE
                        WHEN e.status = 'Paid' THEN 'Paid'
                        WHEN COALESCE((
                            SELECT SUM(p.amount)
                            FROM payments p
                            WHERE p.expense_id = e.id AND NOT p.is_refunded
                        ), 0::numeric) >= e.total_amount - 0.01
                            AND e.total_amount > 0 THEN 'Paid'
                        WHEN COALESCE((
                            SELECT SUM(p.amount)
                            FROM payments p
                            WHERE p.expense_id = e.id AND NOT p.is_refunded
                        ), 0::numeric) > 0.01 THEN 'PartiallyPaid'
                        ELSE 'Unpaid'
                    END,
                    last_pipeline_transition_at = e.updated_at;

                UPDATE expenses AS e
                SET status = CASE
                    WHEN e.approval_status = 'Cancelled' THEN 'Cancelled'
                    WHEN e.approval_status = 'Rejected' THEN 'Rejected'
                    WHEN e.settlement_status = 'Paid' THEN 'Paid'
                    WHEN e.approval_status = 'Approved' THEN 'Approved'
                    WHEN e.processing_status = 'Failed' THEN 'ProcessingFailed'
                    ELSE 'Pending'
                END;

                ALTER TABLE expenses ALTER COLUMN approval_status DROP DEFAULT;
                ALTER TABLE expenses ALTER COLUMN processing_status DROP DEFAULT;
                ALTER TABLE expenses ALTER COLUMN settlement_status DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE expenses
                SET status = CASE status WHEN 'ProcessingFailed' THEN 'Pending' ELSE status END;
                """);

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "last_pipeline_transition_at",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "processing_failed_at",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "processing_failure_reason",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "processing_retry_count",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "processing_status",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "settlement_status",
                table: "expenses");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "expenses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24);
        }
    }
}
