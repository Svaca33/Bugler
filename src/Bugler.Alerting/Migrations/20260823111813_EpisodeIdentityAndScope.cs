using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <summary>
    /// An Episode stops being one Service's (ADR 0034) and its Fingerprint stops being legible
    /// (ADR 0033). The data moves rather than being thrown away: the old Fingerprint was readable,
    /// which is the whole trick — it becomes the Title, and every legacy row is stamped recipe
    /// version 0 so nothing ever re-fingerprints it.
    ///
    /// One-way on purpose. Open Logs Episodes are Muted as Regrouped, because their Fingerprints
    /// belong to a partition that no longer exists and an Acknowledged one would otherwise never
    /// close at all; live acknowledgements and machine claims fall with them, and the Journals
    /// keep what happened. Take a database backup before the first production run.
    /// </summary>
    public partial class EpisodeIdentityAndScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Episodes: the Service becomes evidence, the Scope becomes the binding --------

            // Renamed rather than dropped and re-added: the column already holds the answer to
            // "whose Match opened this", which is exactly what the new name means.
            migrationBuilder.RenameColumn(
                name: "service_id",
                schema: "alerting",
                table: "episodes",
                newName: "opened_by_service_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "opened_by_service_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.DropIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.AddColumn<string>(
                name: "scope_key",
                schema: "alerting",
                table: "episodes",
                type: "character varying(700)",
                maxLength: 700,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "alerting",
                table: "episodes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "recipe_version",
                schema: "alerting",
                table: "episodes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<short>(
                name: "fingerprint_rung",
                schema: "alerting",
                table: "episodes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<bool>(
                name: "stack_truncated",
                schema: "alerting",
                table: "episodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "alert_folded_into_storm",
                schema: "alerting",
                table: "episodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The old Fingerprint was the sender's message template, or the body with its values
            // blanked — the Message rung of the new ladder, and readable, so it becomes the Title
            // unchanged. Every legacy row is bound by its own Service: it was, and a Scope key is
            // written once and never recomputed.
            migrationBuilder.Sql(
                """
                UPDATE alerting.episodes
                SET title = fingerprint,
                    recipe_version = 0,
                    fingerprint_rung = 4,
                    scope_key = 'service=' || opened_by_service_id::text
                """);

            // ---- Participations: who was in each Episode -------------------------------------

            migrationBuilder.CreateTable(
                name: "participations",
                schema: "alerting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    episode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    first_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    warn_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participations", x => x.id);
                    table.ForeignKey(
                        name: "fk_participations_episodes_episode_id",
                        column: x => x.episode_id,
                        principalSchema: "alerting",
                        principalTable: "episodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // One synthetic Participation per legacy Episode: its Service, no version (nothing
            // recorded one until now), and the Episode's own tally and times. Every Episode must
            // hold at least one — the Deletion cascade takes an Episode down with its last.
            migrationBuilder.Sql(
                """
                INSERT INTO alerting.participations
                    (id, episode_id, service_id, version, first_at, last_at, error_count, warn_count)
                SELECT gen_random_uuid(), id, opened_by_service_id, NULL,
                       opened_at, last_match_at, error_count, warn_count
                FROM alerting.episodes
                WHERE opened_by_service_id IS NOT NULL
                """);

            // ---- The re-partition: open Logs Episodes are Muted as Regrouped -----------------

            // A machine claim on a closed Episode holds nothing, and here is where it stops
            // pretending to. The Journal line goes in first, so no claim vanishes without it.
            migrationBuilder.Sql(
                """
                INSERT INTO alerting.journal_entries (episode_id, kind, user_id, delegation_id, at)
                SELECT id, 7, claimed_by_user_id, claimed_by_delegation_id, now()
                FROM alerting.episodes
                WHERE closed_at IS NULL AND watch = 1
                  AND claimed_by_delegation_id IS NOT NULL AND claimed_by_user_id IS NOT NULL
                """);

            migrationBuilder.Sql(
                """
                UPDATE alerting.episodes
                SET closed_at = now(),
                    close_reason = 4,
                    claimed_by_delegation_id = NULL,
                    claimed_by_user_id = NULL,
                    claimed_at = NULL,
                    claim_lease_until = NULL
                WHERE closed_at IS NULL AND watch = 1
                """);

            // An Alert about trouble in a partition that no longer exists is the stale panic the
            // time-to-live exists to prevent.
            migrationBuilder.Sql(
                """
                UPDATE alerting.deliveries
                SET lapsed_at = now(),
                    last_error = 'The application was regrouped before this message left.'
                WHERE delivered_at IS NULL AND lapsed_at IS NULL
                  AND episode_id IN (
                    SELECT id FROM alerting.episodes WHERE close_reason = 4)
                """);

            // ---- Quiet Window overrides: re-keyed on the Scope -------------------------------

            // Every row goes. They were keyed on Fingerprints the new recipe will never mint
            // again, so re-keying them would carry a window over to trouble that no longer has a
            // name. Somebody sets them again on the Episodes that recur, which is the point.
            migrationBuilder.Sql("DELETE FROM alerting.fingerprint_quiet_windows");

            migrationBuilder.DropPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows");

            migrationBuilder.DropColumn(
                name: "service_id",
                schema: "alerting",
                table: "fingerprint_quiet_windows");

            migrationBuilder.AddColumn<string>(
                name: "scope_key",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                type: "character varying(700)",
                maxLength: 700,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                columns: new[] { "scope_key", "fingerprint" });

            // ---- The Fingerprint Rule and the Episode Scope ----------------------------------

            migrationBuilder.AddColumn<short>(
                name: "fingerprint_rule",
                schema: "alerting",
                table: "application_settings",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fingerprint_attribute_key",
                schema: "alerting",
                table: "application_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "scope_by_namespace",
                schema: "alerting",
                table: "application_settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "scope_by_environment",
                schema: "alerting",
                table: "application_settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "scope_by_service_name",
                schema: "alerting",
                table: "application_settings",
                type: "boolean",
                nullable: true);

            // ---- The two new messages --------------------------------------------------------

            migrationBuilder.AddColumn<Guid>(
                name: "joining_service_id",
                schema: "alerting",
                table: "deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "folded_episode_count",
                schema: "alerting",
                table: "deliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries",
                columns: new[] { "episode_id", "kind", "channel", "user_id" },
                unique: true,
                filter: "kind IN (1, 2, 4, 5)")
                .Annotation("Npgsql:NullsDistinct", false);

            // ---- The indexes the Scope now carries -------------------------------------------

            migrationBuilder.CreateIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "scope_key", "fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "scope_key", "watch", "fingerprint" },
                unique: true,
                filter: "closed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_participations_episode_id_service_id_version",
                schema: "alerting",
                table: "participations",
                columns: new[] { "episode_id", "service_id", "version" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_participations_service_id",
                schema: "alerting",
                table: "participations",
                column: "service_id");
        }

        /// <summary>
        /// The schema comes back; what the re-partition decided does not. Muted Episodes stay
        /// Muted and the dropped Quiet Window overrides stay dropped — this exists so a failed
        /// upgrade can be rolled back, not so a decision can be unmade.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participations",
                schema: "alerting");

            migrationBuilder.DropIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "scope_key",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "recipe_version",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "fingerprint_rung",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "stack_truncated",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "alert_folded_into_storm",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "joining_service_id",
                schema: "alerting",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "folded_episode_count",
                schema: "alerting",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "fingerprint_rule",
                schema: "alerting",
                table: "application_settings");

            migrationBuilder.DropColumn(
                name: "fingerprint_attribute_key",
                schema: "alerting",
                table: "application_settings");

            migrationBuilder.DropColumn(
                name: "scope_by_namespace",
                schema: "alerting",
                table: "application_settings");

            migrationBuilder.DropColumn(
                name: "scope_by_environment",
                schema: "alerting",
                table: "application_settings");

            migrationBuilder.DropColumn(
                name: "scope_by_service_name",
                schema: "alerting",
                table: "application_settings");

            // An Episode whose opening Service was Deleted has no Service to go back to; the
            // column was not nullable before, so those rows cannot be carried across.
            migrationBuilder.Sql(
                "DELETE FROM alerting.episodes WHERE opened_by_service_id IS NULL");

            migrationBuilder.AlterColumn<Guid>(
                name: "opened_by_service_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "opened_by_service_id",
                schema: "alerting",
                table: "episodes",
                newName: "service_id");

            migrationBuilder.Sql("DELETE FROM alerting.fingerprint_quiet_windows");

            migrationBuilder.DropPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows");

            migrationBuilder.DropColumn(
                name: "scope_key",
                schema: "alerting",
                table: "fingerprint_quiet_windows");

            migrationBuilder.AddColumn<Guid>(
                name: "service_id",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                columns: new[] { "service_id", "fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "service_id", "fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_one_open_per_kind",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "service_id", "watch", "fingerprint" },
                unique: true,
                filter: "closed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries",
                columns: new[] { "episode_id", "kind", "channel", "user_id" },
                unique: true,
                filter: "kind IN (1, 2)")
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
