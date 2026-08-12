using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineHand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries");

            migrationBuilder.AddColumn<Guid>(
                name: "delegation_id",
                schema: "alerting",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claim_lease_until",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_at",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "claimed_by_delegation_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "claimed_by_user_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "note_by_delegation_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note_link",
                schema: "alerting",
                table: "episodes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note_text",
                schema: "alerting",
                table: "episodes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "noted_at",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proposal_by_delegation_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proposal_link",
                schema: "alerting",
                table: "episodes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "proposal_matches_when_laid",
                schema: "alerting",
                table: "episodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "proposed_at",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resignation_reason",
                schema: "alerting",
                table: "episodes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resigned_at",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resigned_by_delegation_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "claim_lease_hours",
                schema: "alerting",
                table: "application_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_episodes_claim_lease_until",
                schema: "alerting",
                table: "episodes",
                column: "claim_lease_until",
                filter: "claimed_by_delegation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries",
                columns: new[] { "episode_id", "kind", "channel", "user_id" },
                unique: true,
                filter: "kind IN (1, 2)")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_settings_claim_lease",
                schema: "alerting",
                table: "application_settings",
                sql: "claim_lease_hours >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_claim_lease_until",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_application_settings_claim_lease",
                schema: "alerting",
                table: "application_settings");

            migrationBuilder.DropColumn(
                name: "delegation_id",
                schema: "alerting",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "claim_lease_until",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "claimed_by_delegation_id",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "claimed_by_user_id",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "note_by_delegation_id",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "note_link",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "note_text",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "noted_at",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "proposal_by_delegation_id",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "proposal_link",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "proposal_matches_when_laid",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "proposed_at",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "resignation_reason",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "resigned_at",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "resigned_by_delegation_id",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "claim_lease_hours",
                schema: "alerting",
                table: "application_settings");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries",
                columns: new[] { "episode_id", "kind", "channel", "user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
