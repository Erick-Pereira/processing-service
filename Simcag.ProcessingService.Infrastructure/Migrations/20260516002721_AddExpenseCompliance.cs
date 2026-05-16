using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_compliance_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_compliance_comments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_compliance_findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    detail_json = table.Column<string>(type: "jsonb", nullable: true),
                    evidence_document_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    evaluated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    waived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    waived_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    waived_by_user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    waived_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_compliance_findings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_compliance_comments_finding_id",
                table: "expense_compliance_comments",
                column: "finding_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_compliance_comments_tenant_expense",
                table: "expense_compliance_comments",
                columns: new[] { "tenant_id", "expense_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_compliance_tenant_status",
                table: "expense_compliance_findings",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_expense_compliance_tenant_expense_rule",
                table: "expense_compliance_findings",
                columns: new[] { "tenant_id", "expense_id", "rule_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_compliance_comments");

            migrationBuilder.DropTable(
                name: "expense_compliance_findings");
        }
    }
}
