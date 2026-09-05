using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <summary>
    /// A kind of trouble is the Watch and the Fingerprint together inside an Episode Scope (ADR
    /// 0007), and from here the schema says so everywhere rather than only in the one-open-per-kind
    /// invariant. The kind-history index — what every cross-Episode question about a kind reads —
    /// gains the Watch, and so does the primary key of `fingerprint_quiet_windows`, whose own doc
    /// comment already claimed to be keyed on the pair an Episode is told apart by.
    ///
    /// Nothing observable moves. The two Watches build their Scope keys with different prefixes
    /// (`app=…` for the Logs Watch, `service=…` for the Health Check Watch, `EpisodeScope`), so no
    /// Scope key has ever held both and the old answers were already right. That accident is what
    /// the backfill below is allowed to lean on — once, here, to name each existing override's
    /// Watch — and what this migration retires: a third Watch would break it silently.
    /// </summary>
    public partial class KindOfTroubleIsToldByItsWatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows");

            // As on the Episode itself: the default backfills the existing rows and then goes —
            // the column is a fact every writer states, not one the database guesses. Most
            // overrides are the Logs Watch's, so that is what it fills with.
            migrationBuilder.AddColumn<short>(
                name: "watch",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            // A Health Check Episode is always its own Service's (ADR 0034) and nothing else keys
            // that way, so the prefix answers exactly — the last use of the accident this retires.
            migrationBuilder.Sql(
                """
                UPDATE alerting.fingerprint_quiet_windows
                SET watch = 2
                WHERE scope_key LIKE 'service=%'
                """);

            migrationBuilder.Sql(
                "ALTER TABLE alerting.fingerprint_quiet_windows ALTER COLUMN watch DROP DEFAULT;");

            migrationBuilder.AddPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                columns: new[] { "scope_key", "watch", "fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "scope_key", "watch", "fingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows");

            // No row is lost on the way back: the same prefixes that let the backfill be exact
            // mean one Scope key never held two Watches, so dropping the column collides nothing.
            migrationBuilder.DropColumn(
                name: "watch",
                schema: "alerting",
                table: "fingerprint_quiet_windows");

            migrationBuilder.AddPrimaryKey(
                name: "pk_fingerprint_quiet_windows",
                schema: "alerting",
                table: "fingerprint_quiet_windows",
                columns: new[] { "scope_key", "fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_kind_history",
                schema: "alerting",
                table: "episodes",
                columns: new[] { "scope_key", "fingerprint" });
        }
    }
}
