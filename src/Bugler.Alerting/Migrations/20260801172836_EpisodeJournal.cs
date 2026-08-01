using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <inheritdoc />
    public partial class EpisodeJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "alerting",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    episode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_journal_entries_episodes_episode_id",
                        column: x => x.episode_id,
                        principalSchema: "alerting",
                        principalTable: "episodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_episode_id",
                schema: "alerting",
                table: "journal_entries",
                column: "episode_id");

            // Backfill (ADR 0006): the live marks held at upgrade become the entries they would
            // have written — kind 1 = Acknowledged, 3 = Solved. Marks withdrawn or consumed
            // before the Journal existed are gone and stay gone.
            migrationBuilder.Sql(
                """
                INSERT INTO alerting.journal_entries (episode_id, kind, user_id, at)
                SELECT id, 1, acknowledged_by_user_id, acknowledged_at
                FROM alerting.episodes
                WHERE acknowledged_by_user_id IS NOT NULL
                UNION ALL
                SELECT id, 3, solved_by_user_id, solved_at
                FROM alerting.episodes
                WHERE solved_by_user_id IS NOT NULL
                ORDER BY 4;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "alerting");
        }
    }
}
