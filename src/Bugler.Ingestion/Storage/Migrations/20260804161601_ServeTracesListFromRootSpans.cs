using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ServeTracesListFromRootSpans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_spans_roots",
                schema: "telemetry",
                table: "spans",
                column: "start_time",
                descending: new bool[0],
                filter: "parent_span_id IS NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "service_id", "trace_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_spans_roots",
                schema: "telemetry",
                table: "spans");
        }
    }
}
