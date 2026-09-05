using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <summary>
    /// The mark that files a dealt-with Episode away (CONTEXT.md: Archived): an instant and the
    /// User whose hand did it, exactly as the acknowledgement is laid. Two nullable columns and
    /// nothing else — every existing Episode comes through unfiled, which is where they all were.
    ///
    /// No state column moves and none is added: `EpisodeState` stays derived from how the stretch
    /// ended, so a filed Episode can still say whether it was Solved or merely Quieted.
    /// </summary>
    public partial class ArchivedEpisodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at",
                schema: "alerting",
                table: "episodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "archived_by_user_id",
                schema: "alerting",
                table: "episodes",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archived_at",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "archived_by_user_id",
                schema: "alerting",
                table: "episodes");
        }
    }
}
