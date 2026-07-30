using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ServeLogListFromTimestampIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_spans_attributes",
                schema: "telemetry",
                table: "spans");

            migrationBuilder.DropIndex(
                name: "ix_log_records_attributes",
                schema: "telemetry",
                table: "log_records");

            migrationBuilder.CreateIndex(
                name: "ix_log_records_timestamp_service_id_severity_number",
                schema: "telemetry",
                table: "log_records",
                columns: new[] { "timestamp", "service_id", "severity_number" },
                descending: new[] { true, false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_log_records_timestamp_service_id_severity_number",
                schema: "telemetry",
                table: "log_records");

            migrationBuilder.CreateIndex(
                name: "ix_spans_attributes",
                schema: "telemetry",
                table: "spans",
                column: "attributes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_log_records_attributes",
                schema: "telemetry",
                table: "log_records",
                column: "attributes")
                .Annotation("Npgsql:IndexMethod", "gin");
        }
    }
}
