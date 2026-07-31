using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <inheritdoc />
    public partial class EpisodeVerdicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "acknowledged_at",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "acknowledged_by_user_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "solved_at",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "solved_by_user_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "service_id", "fingerprint" });

            // ADR 0003 retired the All Clear (kind = 2): whatever was still owed at upgrade
            // time is owed no more — lapse it rather than let the runner compose a retired kind.
            migrationBuilder.Sql(
                """
                UPDATE alerting.deliveries
                SET lapsed_at = now(), last_error = 'The All Clear was retired (ADR 0003).'
                WHERE kind = 2 AND delivered_at IS NULL AND lapsed_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "acknowledged_at",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "acknowledged_by_user_id",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "solved_at",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "solved_by_user_id",
                schema: "alerting",
                table: "episodes");
        }
    }
}
