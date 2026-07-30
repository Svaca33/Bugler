using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <summary>
    /// Keeps the visibility map current enough for the Exploration queries to stay on their indexes
    /// (ADR 0012). An index-only scan is only "only" over pages VACUUM has marked all-visible, and
    /// telemetry is written and never updated, so autovacuum's proportional defaults stretch the
    /// interval as the table grows: at a retention's worth of rows, over a day of the newest
    /// telemetry would carry no marks — which is exactly the range every query asks about, and the
    /// planner answers by reading the whole table instead. A flat one percent holds the interval
    /// near an hour whatever the table weighs, and each run stays cheap because VACUUM skips the
    /// pages already marked.
    /// </summary>
    public partial class TuneTelemetryAutovacuum : Migration
    {
        /// <summary>
        /// Both telemetry tables, and both settings. The insert one is what the log list and its
        /// Volume lean on; the dead-tuple one is what lets the hourly Purge hand its space back for
        /// reuse, rather than growing a table the retention is supposed to bound.
        /// </summary>
        private static readonly string[] Tables = ["telemetry.log_records", "telemetry.spans"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} SET (
                        autovacuum_vacuum_insert_scale_factor = 0.01,
                        autovacuum_vacuum_scale_factor = 0.01)
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} RESET (
                        autovacuum_vacuum_insert_scale_factor,
                        autovacuum_vacuum_scale_factor)
                    """);
            }
        }
    }
}
