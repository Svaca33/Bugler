using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bugler.Ingestion.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddReleases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "releases",
                schema: "telemetry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    previous_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    observed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_releases", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_releases_service_id_observed_at",
                schema: "telemetry",
                table: "releases",
                columns: new[] { "service_id", "observed_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "releases",
                schema: "telemetry");
        }
    }
}
