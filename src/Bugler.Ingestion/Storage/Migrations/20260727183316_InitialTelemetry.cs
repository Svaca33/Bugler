using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "telemetry");

            migrationBuilder.CreateTable(
                name: "log_records",
                schema: "telemetry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    observed_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    severity_number = table.Column<short>(type: "smallint", nullable: false),
                    severity_text = table.Column<string>(type: "text", nullable: true),
                    body = table.Column<string>(type: "text", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    span_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    service_name = table.Column<string>(type: "text", nullable: true),
                    scope_name = table.Column<string>(type: "text", nullable: true),
                    resource_attributes = table.Column<string>(type: "jsonb", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_log_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_log_records_attributes",
                schema: "telemetry",
                table: "log_records",
                column: "attributes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_log_records_instance_id_timestamp",
                schema: "telemetry",
                table: "log_records",
                columns: new[] { "instance_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_log_records_trace_id",
                schema: "telemetry",
                table: "log_records",
                column: "trace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_records",
                schema: "telemetry");
        }
    }
}
