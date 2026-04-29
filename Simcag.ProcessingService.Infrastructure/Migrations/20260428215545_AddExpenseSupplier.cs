using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CondominioId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Region = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    LowConfidence = table.Column<bool>(type: "boolean", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CondominioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CondominioId_Category",
                table: "Expenses",
                columns: new[] { "CondominioId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CondominioId_Date",
                table: "Expenses",
                columns: new[] { "CondominioId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_RawDocumentId",
                table: "Expenses",
                column: "RawDocumentId",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
