using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertingPollIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_log_records_alerting_poll",
                schema: "telemetry",
                table: "log_records",
                column: "id",
                filter: "severity_number >= 13");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_log_records_alerting_poll",
                schema: "telemetry",
                table: "log_records");
        }
    }
}
