using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <summary>
    /// Signals are stamped with their authenticated Service (ADR 0006). Dropping service_name
    /// loses nothing: it was a copy of the sender's Declared Identity, which still sits in
    /// resource_attributes — the down migration can recreate the column but not repopulate it.
    /// </summary>
    public partial class RenameInstanceToService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "service_name",
                schema: "telemetry",
                table: "spans");

            migrationBuilder.DropColumn(
                name: "service_name",
                schema: "telemetry",
                table: "log_records");

            migrationBuilder.RenameColumn(
                name: "instance_id",
                schema: "telemetry",
                table: "spans",
                newName: "service_id");

            migrationBuilder.RenameIndex(
                name: "ix_spans_instance_id_start_time",
                schema: "telemetry",
                table: "spans",
                newName: "ix_spans_service_id_start_time");

            migrationBuilder.RenameColumn(
                name: "instance_id",
                schema: "telemetry",
                table: "log_records",
                newName: "service_id");

            migrationBuilder.RenameIndex(
                name: "ix_log_records_instance_id_timestamp",
                schema: "telemetry",
                table: "log_records",
                newName: "ix_log_records_service_id_timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "service_id",
                schema: "telemetry",
                table: "spans",
                newName: "instance_id");

            migrationBuilder.RenameIndex(
                name: "ix_spans_service_id_start_time",
                schema: "telemetry",
                table: "spans",
                newName: "ix_spans_instance_id_start_time");

            migrationBuilder.RenameColumn(
                name: "service_id",
                schema: "telemetry",
                table: "log_records",
                newName: "instance_id");

            migrationBuilder.RenameIndex(
                name: "ix_log_records_service_id_timestamp",
                schema: "telemetry",
                table: "log_records",
                newName: "ix_log_records_instance_id_timestamp");

            migrationBuilder.AddColumn<string>(
                name: "service_name",
                schema: "telemetry",
                table: "spans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_name",
                schema: "telemetry",
                table: "log_records",
                type: "text",
                nullable: true);
        }
    }
}
