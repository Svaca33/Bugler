using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <summary>
    /// The Episode stops being a thing only Log Records can open. The four evidence columns are
    /// renamed rather than dropped and re-added — the scaffolder's version would have thrown every
    /// Episode's first log away and, worse, renamed first_log_severity into watch, turning a
    /// severity number into a Watch value. Every Episode that exists was opened by the Logs Watch.
    /// </summary>
    public partial class WatchOnEpisodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "first_log_id",
                schema: "alerting",
                table: "episodes",
                newName: "first_match_log_id");

            migrationBuilder.RenameColumn(
                name: "first_log_timestamp",
                schema: "alerting",
                table: "episodes",
                newName: "first_match_at");

            migrationBuilder.RenameColumn(
                name: "first_log_severity",
                schema: "alerting",
                table: "episodes",
                newName: "first_match_severity");

            migrationBuilder.RenameColumn(
                name: "first_log_body",
                schema: "alerting",
                table: "episodes",
                newName: "first_match_detail");

            // A Watch that does not deal in Log Records leaves these empty.
            migrationBuilder.AlterColumn<long>(
                name: "first_match_log_id",
                schema: "alerting",
                table: "episodes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<short>(
                name: "first_match_severity",
                schema: "alerting",
                table: "episodes",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            // Detection has always stored left(body, 500); the cap only says so out loud.
            migrationBuilder.AlterColumn<string>(
                name: "first_match_detail",
                schema: "alerting",
                table: "episodes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // The default backfills the existing rows and then goes: the column is a fact every
            // writer states, not one the database guesses.
            migrationBuilder.AddColumn<short>(
                name: "watch",
                schema: "alerting",
                table: "episodes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.Sql("ALTER TABLE alerting.episodes ALTER COLUMN watch DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "watch",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.AlterColumn<string>(
                name: "first_match_detail",
                schema: "alerting",
                table: "episodes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            // Going back needs the columns to be total again; Episodes opened by another Watch
            // have nothing to put here, so they take the same placeholders a missing log would.
            migrationBuilder.Sql(
                "UPDATE alerting.episodes SET first_match_log_id = 0 WHERE first_match_log_id IS NULL;");
            migrationBuilder.Sql(
                "UPDATE alerting.episodes SET first_match_severity = 0 WHERE first_match_severity IS NULL;");

            migrationBuilder.AlterColumn<long>(
                name: "first_match_log_id",
                schema: "alerting",
                table: "episodes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "first_match_severity",
                schema: "alerting",
                table: "episodes",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "first_match_detail",
                schema: "alerting",
                table: "episodes",
                newName: "first_log_body");

            migrationBuilder.RenameColumn(
                name: "first_match_severity",
                schema: "alerting",
                table: "episodes",
                newName: "first_log_severity");

            migrationBuilder.RenameColumn(
                name: "first_match_at",
                schema: "alerting",
                table: "episodes",
                newName: "first_log_timestamp");

            migrationBuilder.RenameColumn(
                name: "first_match_log_id",
                schema: "alerting",
                table: "episodes",
                newName: "first_log_id");
        }
    }
}
