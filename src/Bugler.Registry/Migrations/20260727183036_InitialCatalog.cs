using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Registry.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "registry");

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instances",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instances", x => x.id);
                    table.ForeignKey(
                        name: "fk_instances_applications_application_id",
                        column: x => x.application_id,
                        principalSchema: "registry",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "fk_api_keys_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "registry",
                        principalTable: "instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_instance_id",
                schema: "registry",
                table: "api_keys",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_key_hash",
                schema: "registry",
                table: "api_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_name",
                schema: "registry",
                table: "applications",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instances_application_id_name",
                schema: "registry",
                table: "instances",
                columns: new[] { "application_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "instances",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "registry");
        }
    }
}
