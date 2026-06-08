using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMarketBenchmarkFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BenchmarkSource",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogNormalizedName",
                table: "Products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastBenchmarkAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketBenchmarkPrice",
                table: "Products",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketDeviationPercentage",
                table: "Products",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CatalogNormalizedName_Source",
                table: "Products",
                columns: new[] { "CatalogNormalizedName", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_CatalogNormalizedName_Source",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BenchmarkSource",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CatalogNormalizedName",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastBenchmarkAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MarketBenchmarkPrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MarketDeviationPercentage",
                table: "Products");
        }
    }
}
