using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddSpans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spans",
                schema: "telemetry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    span_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    parent_span_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status_code = table.Column<short>(type: "smallint", nullable: false),
                    status_message = table.Column<string>(type: "text", nullable: true),
                    service_name = table.Column<string>(type: "text", nullable: true),
                    scope_name = table.Column<string>(type: "text", nullable: true),
                    resource_attributes = table.Column<string>(type: "jsonb", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    events = table.Column<string>(type: "jsonb", nullable: false),
                    links = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spans", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_spans_attributes",
                schema: "telemetry",
                table: "spans",
                column: "attributes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_spans_instance_id_start_time",
                schema: "telemetry",
                table: "spans",
                columns: new[] { "instance_id", "start_time" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_spans_trace_id",
                schema: "telemetry",
                table: "spans",
                column: "trace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spans",
                schema: "telemetry");
        }
    }
}
