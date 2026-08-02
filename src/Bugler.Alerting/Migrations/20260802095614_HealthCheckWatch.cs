using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <summary>
    /// The Health Check Watch's one setting, and the Watch joining the one-open-per-kind key: a
    /// Fingerprint means something different under each Watch, so a log whose body happens to
    /// read like the reserved health check kind is different trouble, not the same open Episode.
    /// </summary>
    public partial class HealthCheckWatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.AddColumn<string>(
                name: "health_check_url",
                schema: "alerting",
                table: "service_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "service_id", "watch", "fingerprint" },
                unique: true,
                filter: "closed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "health_check_url",
                schema: "alerting",
                table: "service_settings");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "service_id", "fingerprint" },
                unique: true,
                filter: "closed_at IS NULL");
        }
    }
}
