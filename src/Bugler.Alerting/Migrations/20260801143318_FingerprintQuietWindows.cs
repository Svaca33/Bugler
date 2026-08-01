using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <inheritdoc />
    public partial class FingerprintQuietWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fingerprint_quiet_windows",
                schema: "alerting",
                columns: table => new
                {
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiet_window_minutes = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fingerprint_quiet_windows", x => new { x.service_id, x.fingerprint });
                    table.CheckConstraint("ck_fingerprint_quiet_windows_bounds", "quiet_window_minutes BETWEEN 1 AND 10080");
                });

            migrationBuilder.CreateIndex(
                name: "ix_fingerprint_quiet_windows_application_id",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                column: "application_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fingerprint_quiet_windows",
                schema: "alerting");
        }
    }
}
