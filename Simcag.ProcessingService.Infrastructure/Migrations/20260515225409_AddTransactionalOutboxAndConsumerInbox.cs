using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.ProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalOutboxAndConsumerInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumer_inbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_group = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    transport_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumer_inbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    routing_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    trace_parent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    trace_state = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    baggage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    poisoned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_outbox", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consumer_inbox_tenant_group_received",
                table: "consumer_inbox",
                columns: new[] { "tenant_id", "consumer_group", "received_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_consumer_inbox_group_transport",
                table: "consumer_inbox",
                columns: new[] { "consumer_group", "transport_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_message_outbox_status_next_attempt",
                table: "message_outbox",
                columns: new[] { "status", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_message_outbox_message_id",
                table: "message_outbox",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_message_outbox_tenant_dedupe",
                table: "message_outbox",
                columns: new[] { "tenant_id", "dedupe_key" },
                unique: true,
                filter: "dedupe_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumer_inbox");

            migrationBuilder.DropTable(
                name: "message_outbox");
        }
    }
}
