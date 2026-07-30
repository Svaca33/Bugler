using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <inheritdoc />
    public partial class EpisodePerKindOfTrouble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_one_open_per_service",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.AddColumn<string>(
                name: "fingerprint",
                schema: "alerting",
                table: "episodes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "service_id", "fingerprint" },
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
                name: "fingerprint",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_one_open_per_service",
                schema: "alerting",
                table: "episodes",
                column: "service_id",
                unique: true,
                filter: "closed_at IS NULL");
        }
    }
}
