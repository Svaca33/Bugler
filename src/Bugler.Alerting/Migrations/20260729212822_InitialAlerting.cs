using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <inheritdoc />
    public partial class InitialAlerting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "alerting");

            migrationBuilder.CreateTable(
                name: "application_settings",
                schema: "alerting",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sensitivity = table.Column<short>(type: "smallint", nullable: true),
                    quiet_window_minutes = table.Column<int>(type: "integer", nullable: true),
                    chat_webhook_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_settings", x => x.application_id);
                    table.CheckConstraint("ck_application_settings_quiet_window", "quiet_window_minutes >= 1");
                });

            migrationBuilder.CreateTable(
                name: "episodes",
                schema: "alerting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_log_id = table.Column<long>(type: "bigint", nullable: false),
                    first_log_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_log_severity = table.Column<short>(type: "smallint", nullable: false),
                    first_log_body = table.Column<string>(type: "text", nullable: true),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    warn_count = table.Column<int>(type: "integer", nullable: false),
                    last_match_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    close_reason = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_episodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "poll_cursor",
                schema: "alerting",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    last_log_id = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poll_cursor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seen_log_ids",
                schema: "alerting",
                columns: table => new
                {
                    log_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seen_log_ids", x => x.log_id);
                });

            migrationBuilder.CreateTable(
                name: "service_settings",
                schema: "alerting",
                columns: table => new
                {
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sensitivity = table.Column<short>(type: "smallint", nullable: true),
                    quiet_window_minutes = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_settings", x => x.service_id);
                    table.CheckConstraint("ck_service_settings_quiet_window", "quiet_window_minutes >= 1");
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "alerting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.CheckConstraint("ck_subscriptions_one_target", "(application_id IS NULL) <> (service_id IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                schema: "alerting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    episode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    channel = table.Column<short>(type: "smallint", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lapsed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliveries", x => x.id);
                    table.CheckConstraint("ck_deliveries_mail_names_a_user", "(channel = 1) = (user_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_deliveries_episodes_episode_id",
                        column: x => x.episode_id,
                        principalSchema: "alerting",
                        principalTable: "episodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_episode_id_kind_channel_user_id",
                schema: "alerting",
                table: "deliveries",
                columns: new[] { "episode_id", "kind", "channel", "user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_next_attempt_at",
                schema: "alerting",
                table: "deliveries",
                column: "next_attempt_at",
                filter: "delivered_at IS NULL AND lapsed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_application_id",
                schema: "alerting",
                table: "episodes",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_one_open_per_service",
                schema: "alerting",
                table: "episodes",
                column: "service_id",
                unique: true,
                filter: "closed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_opened_at_id",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "opened_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_service_settings_application_id",
                schema: "alerting",
                table: "service_settings",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_application_id",
                schema: "alerting",
                table: "subscriptions",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_service_id",
                schema: "alerting",
                table: "subscriptions",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_user_id_application_id",
                schema: "alerting",
                table: "subscriptions",
                columns: new[] { "user_id", "application_id" },
                unique: true,
                filter: "application_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_user_id_service_id",
                schema: "alerting",
                table: "subscriptions",
                columns: new[] { "user_id", "service_id" },
                unique: true,
                filter: "service_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_settings",
                schema: "alerting");

            migrationBuilder.DropTable(
                name: "deliveries",
                schema: "alerting");

            migrationBuilder.DropTable(
                name: "poll_cursor",
                schema: "alerting");

            migrationBuilder.DropTable(
                name: "seen_log_ids",
                schema: "alerting");

            migrationBuilder.DropTable(
                name: "service_settings",
                schema: "alerting");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "alerting");

            migrationBuilder.DropTable(
                name: "episodes",
                schema: "alerting");
        }
    }
}
